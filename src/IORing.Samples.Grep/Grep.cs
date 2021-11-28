using System;
using System.Collections.Concurrent;
using System.Text;
using IORing.Samples.Library;

namespace IORing.Samples.Grep;

[Flags]
public enum GrepOptions
{
    None,
    Debug = 1,
    Dump = 2
}

public class Grep
{
    private readonly string _directory;
    private readonly byte[] _term;
    private readonly int _threadCount;
    private readonly int _buffersize;
    private readonly GrepOptions _options;
    private readonly Thread[] _threads;
    private readonly ConcurrentQueue<GrepWork> _pendingWork = new();
    private readonly AutoResetEvent _availableWorkSignal = new(false);
    private readonly AutoResetEvent _availableBucketsSignal = new(false);
    private readonly ManualResetEvent _allWorkCompletedSignal = new(false);
    private readonly GrepWork[] _grepfiles;
    private readonly int _bucketCount;

    private const int _emptyId = -1;

    public Grep(string directory, string term, int threadCount, int buffersize, GrepOptions options)
    {
        _directory = directory;
        _term = Encoding.UTF8.GetBytes(term);
        _threadCount = threadCount;
        _buffersize = buffersize;
        _options = options;
        _threads = Enumerable.Range(0, threadCount)
            .Select(t => new Thread(Process)
            {
                IsBackground = true,
                Name = $"{nameof(Grep)}.{t}"
            }).ToArray();

        foreach (var thread in _threads)
        {
            thread.Start();
        }

        // The original thought was to queue up more work than there are threads, but in testing, having them be 1:1
        // executed the fastest.
        _bucketCount = threadCount * 1;

        _grepfiles = new GrepWork[_bucketCount];
        for (var i = 0; i < _grepfiles.Length; ++i)
        {
            _grepfiles[i].Id = _emptyId;
        }
    }

    public void Run()
    {

        var debug = _options.HasFlag(GrepOptions.Debug);

        using var buffers = new DisposableList<IORingBuffer>();
        buffers.AddRange(Enumerable.Range(0, _bucketCount).Select(t => new IORingBuffer((uint)_buffersize)));

        using var ioringCompletionSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "IORing");
        using var ioring = HIORING.Create(IORING_VERSION.VERSION_1, _bucketCount, _bucketCount);
        ioring.SetCompletionEvent(ioringCompletionSignal);
        ioring.BuildRegisterBuffers(buffers.Select(t => t.AsBufferInfo()).ToArray(), _emptyId);

        nint fileid = 0;
        var allWorkCompleted = false;

        var pendingFiles = new Stack<(string Filename, int Offset)>();
        foreach (var file in Directory.GetFiles(_directory))
        {
            pendingFiles.Push(new(file, 0));
        }

        while (pendingFiles.Count > 0 && !allWorkCompleted)
        {
            var queuedWorkCount = 0;
            while (pendingFiles.Count > 0 && queuedWorkCount < _bucketCount)
            {
                var checkedBuckets = 0;
                var noBucketsAvailable = false;
                // Check fileid != _emptyId as protection against the extremely unlikely situation where we wrap a 64bit number.
                while (Volatile.Read(ref _grepfiles[fileid % _bucketCount].Id) != _emptyId && fileid != _emptyId)
                {
                    ++fileid;
                    if (++checkedBuckets == _bucketCount)
                    {
                        noBucketsAvailable = true;
                        break;
                    }
                }

                // All buckets full. Submit work.
                if (noBucketsAvailable)
                {
                    // If no work was able to be queued and we ran out of buckets, wait for buckets to become available.
                    if (queuedWorkCount == 0)
                    {
                        _availableBucketsSignal.WaitOne();
                        continue;
                    }

                    break;
                }

                var pendingFile = pendingFiles.Pop();
                var bucket = (int)(fileid % _bucketCount);
                var buffer = buffers[bucket];
                var grepfile = new GrepWork(fileid, bucket, pendingFile.Filename, buffer, pendingFile.Offset);
                _grepfiles[bucket] = grepfile;

                // Using a `IORING_REF_KIND.REGISTERED` vs a `IORING_REF_KIND.RAW` seems to not make a performance
                // difference in my testing.
                var bufferIndex = new IORING_BUFFER_REF((int)bucket, 0);
                ioring.BuildReadFile(grepfile.FileStream, bufferIndex, _buffersize, grepfile.Offset, bucket);

                if (debug) Console.WriteLine($"Requesting offset: {grepfile.Offset}, bucket: {bucket}, file: {pendingFile.Filename}");

                ++queuedWorkCount;
            }

            ioring.Submit();

            var workFound = 0;
            while (!allWorkCompleted)
            {
                while (ioring.TryPopCompletion(out var cqe))
                {
                    // 0 is what we used to register the buffers. It should be ignored here.
                    if (cqe.UserData == _emptyId) continue;

                    var grepfile = _grepfiles[cqe.UserData];
                    grepfile.ByteCount = (int)cqe.Information;

                    if (debug) Console.WriteLine($"Read complete, bytes: {grepfile.ByteCount}, offset: {grepfile.Offset}, result: {((ulong)cqe.ResultCode.Code):X8}, file: {grepfile.Filename}");

                    if (cqe.IsEndOfFile())
                    {
                        if (debug) Console.WriteLine($"Closing file: {grepfile.Filename}");

                        Volatile.Write(ref _grepfiles[grepfile.Bucket].Id, _emptyId);
                        grepfile.Dispose();
                        continue;
                    }

                    _pendingWork.Enqueue(grepfile);
                    ++workFound;

                    // FIX: Arg. This should technically be `- _term.Length + 1` for when the read and the match
                    // boundary overlap but then we never get IsEndOfFile().
                    pendingFiles.Push(new(grepfile.Filename, grepfile.Offset + grepfile.ByteCount));
                }

                // If there's more work to queue up, go back to submitting work.
                if (pendingFiles.Count > 0 || workFound > 0) break;

                if (pendingFiles.Count == 0 && workFound == 0)
                {
                    allWorkCompleted = true;
                    break;
                }

                ioringCompletionSignal.WaitOne();
            }

            if (workFound > 0)
            {
                _availableWorkSignal.Set();
            }
        }

        _allWorkCompletedSignal.Set();
        foreach (var thread in _threads)
        {
            thread.Join();
        }
    }

    private void Process()
    {
        var debug = _options.HasFlag(GrepOptions.Debug);
        var dump = _options.HasFlag(GrepOptions.Dump);
        var term = new Span<byte>(_term);
        const int allWorkCompletedSignalIndex = 0;
        var waitEvents = new WaitHandle[] { _allWorkCompletedSignal, _availableWorkSignal };

        while (true)
        {
            if (!_pendingWork.TryDequeue(out var work))
            {
                if (WaitHandle.WaitAny(waitEvents) == allWorkCompletedSignalIndex)
                {
                    // The IsEmpty check should be race free. _allWorkCompletedSignal is only set once all _pendingWork
                    // has been queued up, and _pendingWork should have strong memory ordering.
                    if (_pendingWork.IsEmpty) return;
                }

                continue;
            }

            var buffer = work.Buffer.AsReadOnlySpan(work.ByteCount);

            if (debug) Console.WriteLine($"Procssing {work.ByteCount} bytes in {work.Filename}");

            if (dump)
            {
                Console.WriteLine($"Have: {HexFormatter.GetHex(buffer)}");
                Console.WriteLine($"Want: {HexFormatter.GetHex(_term)}");
            }

            var indexOffset = work.Offset;

            while (true)
            {
                var index = buffer.IndexOf(term);
                if (index == -1) break;

                Console.WriteLine($"Found at {index + indexOffset} in {work.Filename}");
                buffer = buffer.Slice(index + term.Length);
                indexOffset += index;
            }

            // Mark this bucket as available for use.
            Volatile.Write(ref _grepfiles[work.Bucket].Id, _emptyId);
            _availableBucketsSignal.Set();
        }
    }
}

struct GrepWork : IDisposable
{
    public long Id;
    public readonly int Bucket;
    public readonly FileStream FileStream;
    public readonly string Filename;
    public readonly IORingBuffer Buffer;
    public readonly int Offset;
    public int ByteCount;

    public GrepWork(nint id, int bucket, string filename, IORingBuffer buffer, int offset)
    {
        Id = id;
        Bucket = bucket;
        FileStream = File.OpenRead(filename);
        Filename = filename;
        Buffer = buffer;
        Offset = offset;
        ByteCount = 0;
    }

    public void Dispose()
    {
        FileStream.TryDispose();
    }
}