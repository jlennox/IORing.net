using System;
using System.IO;
using System.Runtime.InteropServices;

namespace IORing.Samples.Library;

[Flags]
public enum AllocationType
{
    Commit = 0x1000,
    Reserve = 0x2000,
    Decommit = 0x4000,
    Release = 0x8000,
    Reset = 0x80000,
    Physical = 0x400000,
    TopDown = 0x100000,
    WriteWatch = 0x200000,
    LargePages = 0x20000000
}

[Flags]
public enum MemoryProtection
{
    Execute = 0x10,
    ExecuteRead = 0x20,
    ExecuteReadWrite = 0x40,
    ExecuteWriteCopy = 0x80,
    NoAccess = 0x01,
    ReadOnly = 0x02,
    ReadWrite = 0x04,
    WriteCopy = 0x08,
    GuardModifierflag = 0x100,
    NoCacheModifierflag = 0x200,
    WriteCombineModifierflag = 0x400
}

public unsafe static class Kernel
{
    public static readonly int NumPhysicalCores = GetProcessorCoreCount();

    public const nint INVALID_HANDLE_VALUE = -1;

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr VirtualAllocEx(
        IntPtr hProcess,
        IntPtr lpAddress,
        IntPtr dwSize,
        AllocationType flAllocationType,
        MemoryProtection flProtect);

    [DllImport("kernel32", SetLastError = true)]
    public static extern IntPtr VirtualAlloc(
        IntPtr lpAddress,
        uint dwSize,
        AllocationType flAllocationType,
        MemoryProtection flProtect);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr CreateFile(
         [MarshalAs(UnmanagedType.LPTStr)] string filename,
         [MarshalAs(UnmanagedType.U4)] FileAccess access,
         [MarshalAs(UnmanagedType.U4)] FileShare share,
         IntPtr securityAttributes, // optional SECURITY_ATTRIBUTES struct or IntPtr.Zero
         [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
         [MarshalAs(UnmanagedType.U4)] FileAttributes flagsAndAttributes,
         IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr CreateEvent(
        IntPtr lpEventAttributes,
        bool bManualReset,
        bool bInitialState,
        string lpName);

    [StructLayout(LayoutKind.Sequential)]
    struct CACHE_DESCRIPTOR
    {
        public byte Level;
        public byte Associativity;
        public ushort LineSize;
        public uint Size;
        public uint Type;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION_UNION
    {
        [FieldOffset(0)] public byte ProcessorCore;
        [FieldOffset(0)] public uint NumaNode;
        [FieldOffset(0)] public CACHE_DESCRIPTOR Cache;
        [FieldOffset(0)] private ulong Reserved1;
        [FieldOffset(8)] private ulong Reserved2;
    }

    public enum LOGICAL_PROCESSOR_RELATIONSHIP
    {
        RelationProcessorCore,
        RelationNumaNode,
        RelationCache,
        RelationProcessorPackage,
        RelationGroup,
        RelationAll = 0xffff
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION
    {
        public UIntPtr ProcessorMask;
        public LOGICAL_PROCESSOR_RELATIONSHIP Relationship;
        public SYSTEM_LOGICAL_PROCESSOR_INFORMATION_UNION ProcessorInformation;
    }

    [DllImport("kernel32.dll")]
    private static extern unsafe bool GetLogicalProcessorInformation(
        SYSTEM_LOGICAL_PROCESSOR_INFORMATION* buffer,
        out int bufferSize);

    private static unsafe int GetProcessorCoreCount()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // TODO: Arg, make this work properly for cores on other platforms.
            // sysconf(_SC_NPROCESSORS_ONLN)? Might work on Windows?
            return Environment.ProcessorCount;
        }

        GetLogicalProcessorInformation(null, out var bufferSize);
        var numEntries = bufferSize / sizeof(SYSTEM_LOGICAL_PROCESSOR_INFORMATION);
        var coreInfo = new SYSTEM_LOGICAL_PROCESSOR_INFORMATION[numEntries];

        fixed (SYSTEM_LOGICAL_PROCESSOR_INFORMATION* pCoreInfo = coreInfo)
        {
            GetLogicalProcessorInformation(pCoreInfo, out bufferSize);
            var cores = 0;
            for (var i = 0; i < numEntries; ++i)
            {
                var info = pCoreInfo[i];
                if (info.Relationship == LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore)
                    ++cores;
            }
            return cores > 0 ? cores : 1;
        }
    }
}