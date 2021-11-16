using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace IORing;

public enum IORING_CREATE_REQUIRED_FLAGS
{
    NONE
}

public enum IORING_CREATE_ADVISORY_FLAGS
{
    NONE
}

public struct IORING_CREATE_FLAGS
{
    public IORING_CREATE_REQUIRED_FLAGS Required;
    public IORING_CREATE_ADVISORY_FLAGS Advisory;

    public override string ToString()
    {
        return $"{Required} + {Advisory}";
    }
}

[Flags]
public enum IORING_FEATURE_FLAGS
{
    NONE,
    UM_EMULATION = 0x00000001,
    SET_COMPLETION_EVENT = 0x00000002,
}

public enum IORING_REF_KIND
{
    RAW,
    REGISTERED
}

public enum IORING_OP_CODE
{
    NOP,
    READ,
    REGISTER_FILES,
    REGISTER_BUFFERS,
    CANCEL
}

public enum IORING_VERSION
{
    INVALID,
    VERSION_1,
    VERSION_2,
}

public enum IORING_SQE_FLAGS
{
    NONE
}

[StructLayout(LayoutKind.Sequential)]
public struct IORING_QUEUE_HEAD
{
    public uint QueueIndex;
    public uint QueueCount;
    public ulong Aligment;
}

[StructLayout(LayoutKind.Sequential)]
public struct IORING_BUFFER_INFO
{
    public IntPtr Address;
    public uint Length;
}

[StructLayout(LayoutKind.Explicit)]
public unsafe struct IORING_BUFFER_REF
{
    [FieldOffset(0)]
    public IORING_REF_KIND Kind;

    [FieldOffset(8)]
    public IntPtr Address;

    [FieldOffset(8)]
    public uint Index;
    [FieldOffset(12)]
    public uint Offset;

    public IORING_BUFFER_REF(IntPtr address) : this()
    {
        Kind = IORING_REF_KIND.RAW;
        Address = address;
    }

    public IORING_BUFFER_REF(byte* address) : this()
    {
        Kind = IORING_REF_KIND.RAW;
        Address = (IntPtr)address;
    }

    public IORING_BUFFER_REF(uint index, uint offset) : this()
    {
        Kind = IORING_REF_KIND.REGISTERED;
        Index = index;
        Offset = offset;
    }

    public IORING_BUFFER_REF(int index, int offset) : this()
    {
        Kind = IORING_REF_KIND.REGISTERED;
        checked
        {
            Index = (uint)index;
            Offset = (uint)offset;
        }
    }
}

[StructLayout(LayoutKind.Explicit)]
public struct IORING_HANDLE_REF
{
    [FieldOffset(0)]
    public IORING_REF_KIND Kind;

    [FieldOffset(8)]
    public HANDLE Handle;

    [FieldOffset(8)]
    public uint Index;

    public IORING_HANDLE_REF(HANDLE handle) : this()
    {
        Kind = IORING_REF_KIND.RAW;
        Handle = handle;
    }

    public IORING_HANDLE_REF(uint index) : this()
    {
        Kind = IORING_REF_KIND.REGISTERED;
        Index = index;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct IORING_INFO
{
    public IORING_VERSION IoRingVersion;
    public IORING_CREATE_FLAGS Flags;
    public uint SubmissionQueueSize;
    public uint CompletionQueueSize;

    public override string ToString()
    {
        return $"IoRingVersion: {IoRingVersion}, Flags: {Flags}, SubmissionQueueSize: {SubmissionQueueSize}, CompletionQueueSize: {CompletionQueueSize}";
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct HANDLE
{
    public IntPtr Ptr;

    public static implicit operator HANDLE(IntPtr d) => new() { Ptr = d };
    public static implicit operator IntPtr(HANDLE d) => d.Ptr;

    public bool TryZeroHandle(out IntPtr ptr)
    {
        ptr = Interlocked.Exchange(ref Ptr, IntPtr.Zero);
        return ptr != IntPtr.Zero;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct IORING_CAPABILITIES
{
    public IORING_VERSION MaxVersion;
    public uint MaxSubmissionQueueSize;
    public uint MaxCompletionQueueSize;
    public IORING_FEATURE_FLAGS FeatureFlags;

    public override string ToString()
    {
        return $"MaxVersion: {MaxVersion}, MaxSubmissionQueueSize: {MaxSubmissionQueueSize}, MaxCompletionQueueSize: {MaxCompletionQueueSize}, FeatureFlags: {FeatureFlags}";
    }
}

/// <summary>
/// Represents a completed I/O ring queue entry.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct IORING_CQE
{
    /// <summary>
    /// A UINT_PTR representing the user data associated with the entry. This is the same value provided as the UserData parameter when building the operation's submission queue entry. Applications can use this value to correlate the completion with the original operation request.
    /// </summary>
    public nint UserData;
    /// <summary>
    /// A HRESULT indicating the result code of the associated I/O ring operation.
    /// </summary>
    public HRESULT ResultCode;
    /// <summary>
    /// A ULONG_PTR representing information about the completed queue operation.
    /// </summary>
    public ulong Information;

    public HRESULT GetResultCode()
    {
        // Example of success:  00007FF800000000
        // Example of an error: 00007FF8800706F8
        // What this means isn't documented. 00007FF8 is clearly a prefix but does not have a documented/known
        // meaning. 800706F8 is the error result.
        return new HRESULT
        {
            Code = (HResultCode)((uint)ResultCode.Code & 0xFFFFFFFF)
        };
    }

    public void Check(string caller)
    {
        GetResultCode().Check(caller);
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct HRESULT
{
    public HResultCode Code;

    public void Check(string caller)
    {
        if (Code == 0) return;
        Marshal.ThrowExceptionForHR((int)Code);
    }
}