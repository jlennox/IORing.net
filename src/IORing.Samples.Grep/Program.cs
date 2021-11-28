using IORing.Samples.Library;

namespace IORing.Samples.Grep;

public class Program
{
    public static void Main(string[] args)
    {
        var directory = args.FirstOrDefault() ?? throw new ArgumentOutOfRangeException("directory");
        var term = args.ElementAtOrDefault(1) ?? throw new ArgumentOutOfRangeException("search term");
        var options = GrepOptions.None;
        var bufferSize = 1 * 1024 * 1024; // Some basic testing shows 1mb is faster than 512kb and faster than 5mb on a sample set of 20mb files.
        var threadCount = Kernel.NumPhysicalCores;

        for (var i = 2; i < args.Length; ++i)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--debug":
                case "-v":
                    options |= GrepOptions.Debug;
                    break;
                case "--dump":
                case "-vv":
                    options |= GrepOptions.Dump;
                    break;
                case "--buffersize":
                    bufferSize = int.Parse(args[++i]);
                    break;
                case "--threads":
                    threadCount = int.Parse(args[++i]);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(arg), arg);
            }
        }

        var grep = new Grep(directory, term, threadCount, bufferSize, options);
        grep.Run();
    }
}

