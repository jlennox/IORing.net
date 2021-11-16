using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using static IORing.KernelBase;

namespace IORing;

internal unsafe class Program
{
    public static void Main(string[] args)
    {
        try
        {
            AsyncIORingTest();
        }
        catch (Exception e)
        {
            Console.WriteLine("Unexpected result: " + e);
        }

        Console.WriteLine("All finished!");
        Console.ReadLine();
    }

    // This is an exmaple of building an I/O ring that sends multiple reads in a single system call.
    public static void AsyncIORingTest()
    {
        const int bufferSize = 1028 * 8;
        const int sizeToRead = 1028 * 8;
        const int readsToPerform = 4;

        Console.WriteLine(QueryIoRingCapabilitiesChecked());
        using var handle = CreateIoRingChecked(IORING_VERSION.VERSION_1, new IORING_CREATE_FLAGS(), 5, 5);
        Console.WriteLine(GetIoRingInfoChecked(handle));

        var buffers = new[] {
            new IORING_BUFFER_INFO { Address = Marshal.AllocHGlobal(bufferSize), Length = bufferSize },
            new IORING_BUFFER_INFO { Address = Marshal.AllocHGlobal(bufferSize), Length = bufferSize },
            new IORING_BUFFER_INFO { Address = Marshal.AllocHGlobal(bufferSize), Length = bufferSize },
            new IORING_BUFFER_INFO { Address = Marshal.AllocHGlobal(bufferSize), Length = bufferSize },
        };

        // Zero out the buffers to make debugging easier.
        foreach (var buffer in buffers)
        {
            Unsafe.InitBlock((void*)buffer.Address, 0, bufferSize);
        }

        // Queue the registering of these buffers and give the userData of -1. The userData will let us know what
        // event maps back to this action.
        handle.BuildRegisterBuffers(buffers, -1);

        using var file = File.OpenRead(@"C:\Windows\System32\notepad.exe");

        // Lets perform multiple reads. To be sure that what we think is happening is happening, each one points
        // one byte further into our local buffer, and one byte further into the file. This way our resulting buffers
        // are not all identical.
        for (uint i = 0; i < readsToPerform; ++i)
        {
            var requestDataBuffer = new IORING_BUFFER_REF(i, i);
            handle.BuildReadFile(file, requestDataBuffer, sizeToRead - i, i, (nint)i);
        }

        using var waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, "IORing");
        handle.SetCompletionEvent(waitHandle);

        var submittedEntries = handle.Submit();
        Console.WriteLine($"submittedEntries: {submittedEntries}");

        var entriesRead = 0;
        var sb = new StringBuilder();
        while (submittedEntries > entriesRead)
        {
            if (!handle.TryPopCompletion(out var cqe))
            {
                Console.WriteLine("Needed to wait.");
                waitHandle.WaitOne();
                continue;
            }

            ++entriesRead;

            // cqe.Information contains either the number of buffers registered, or the number of bytes read.
            Console.WriteLine($"PopCompletion: {cqe.UserData} ({(ulong)cqe.ResultCode.Code:X16}, {cqe.Information})");

            // Skips the BuildIoRingRegisterBuffersChecked result.
            if (cqe.UserData == -1) continue;

            var buffer = buffers[cqe.UserData];
            sb.Append($"Entry {entriesRead}[{cqe.UserData}]:");
            for (var i = 0; i < (int)cqe.Information; ++i)
            {
                sb.Append($" {((byte*)buffer.Address)[i]:X2}");
            }
            Console.WriteLine(sb.ToString());
            sb.Clear();
        }
    }

    public static void BlockingIORingTest()
    {
        Console.WriteLine(QueryIoRingCapabilitiesChecked());
        using var handle = CreateIoRingChecked(IORING_VERSION.VERSION_1, new IORING_CREATE_FLAGS(), 5, 5);
        Console.WriteLine(handle.GetInfo());

        using var file = File.OpenRead(@"C:\Windows\System32\notepad.exe");

        const int sizeToRead = 0x8;
        var buffer = new byte[sizeToRead];
        fixed (byte* bufferPtr = buffer)
        {
            var requestDataBuffer = new IORING_BUFFER_REF(bufferPtr);
            handle.BuildReadFile(file, requestDataBuffer, sizeToRead, 0);

            var submittedEntries = handle.Submit(1, TimeSpan.FromSeconds(1));
            Console.WriteLine($"submittedEntries: {submittedEntries}");
        }

        foreach (var b in buffer)
        {
            Console.WriteLine($"line {b:X2}");
        }
    }
}