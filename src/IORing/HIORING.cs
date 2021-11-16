using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using static IORing.KernelBase;

namespace IORing;

[StructLayout(LayoutKind.Sequential)]
public struct HIORINGHandle : IDisposable
{
    public HANDLE Handle;

    public static implicit operator HIORINGHandle(IntPtr d) => new() { Handle = d };
    public static implicit operator HIORINGHandle(HANDLE d) => new() { Handle = d };
    public static implicit operator IntPtr(HIORINGHandle d) => d.Handle.Ptr;
    public static implicit operator HANDLE(HIORINGHandle d) => d.Handle;

    public void Dispose()
    {
        if (Handle.TryZeroHandle(out var ptr))
        {
            CloseIoRingChecked(ptr);
        }
    }
}

public struct IORingFileRef : IDisposable
{
    private readonly FileStream _fs;

    public IORingFileRef(string filename, FileMode mode, FileAccess access)
    {
        _fs = File.Open(filename, mode, access);
    }

    public IORING_HANDLE_REF GetHandleRef()
    {
        return new IORING_HANDLE_REF(_fs.SafeFileHandle.DangerousGetHandle());
    }

    public void Dispose()
    {
        _fs.Dispose();
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct HIORING : IDisposable
{
    public HANDLE Handle;

    public static implicit operator HIORING(IntPtr d) => new() { Handle = d };
    public static implicit operator HIORING(HANDLE d) => new() { Handle = d };
    public static implicit operator HIORING(HIORINGHandle d) => new() { Handle = d };
    public static implicit operator IntPtr(HIORING d) => d.Handle.Ptr;
    public static implicit operator HANDLE(HIORING d) => d.Handle;
    public static implicit operator HIORINGHandle(HIORING d) => new() { Handle = d.Handle.Ptr };

    public uint Submit(uint waitOperations, TimeSpan timeout)
    {
        return SubmitIoRingChecked(this, waitOperations, timeout);
    }

    /// <inheritdoc cref="SubmitIoRing" />
    public uint Submit()
    {
        return SubmitIoRingChecked(this);
    }

    /// <inheritdoc cref="GetIoRingInfo" />
    public IORING_INFO GetInfo()
    {
        return GetIoRingInfoChecked(this);
    }

    /// <inheritdoc cref="IsIoRingOpSupported" />
    public bool IsOpSupported(IORING_OP_CODE op)
    {
        return IsIoRingOpSupportedChecked(this, op);
    }

    /// <inheritdoc cref="PopIoRingCompletion" />
    public bool TryPopCompletion(out IORING_CQE cqe)
    {
        return TryPopIoRingCompletionChecked(this, out cqe);
    }

    /// <inheritdoc cref="BuildIoRingRegisterBuffers" />
    public void BuildRegisterBuffers(ReadOnlySpan<IORING_BUFFER_INFO> buffers, nint userData = 0)
    {
        BuildIoRingRegisterBuffersChecked(this, buffers, userData);
    }

    /// <inheritdoc cref="BuildIoRingReadFile" />
    public void BuildReadFile(
        IORING_HANDLE_REF fileRef,
        IORING_BUFFER_REF dataRef,
        uint numberOfBytesToRead,
        ulong offset,
        nint userData = 0,
        IORING_SQE_FLAGS flags = IORING_SQE_FLAGS.NONE)
    {
        BuildIoRingReadFileChecked(this, fileRef, dataRef, numberOfBytesToRead, offset, userData, flags);
    }

    /// <inheritdoc cref="BuildIoRingReadFile" />
    public void BuildReadFile(
        FileStream filestream,
        IORING_BUFFER_REF dataRef,
        uint numberOfBytesToRead,
        ulong offset,
        nint userData = 0,
        IORING_SQE_FLAGS flags = IORING_SQE_FLAGS.NONE)
    {
        var fileRef = new IORING_HANDLE_REF(filestream.SafeFileHandle.DangerousGetHandle());
        BuildIoRingReadFileChecked(this, fileRef, dataRef, numberOfBytesToRead, offset, userData, flags);
    }

    /// <inheritdoc cref="BuildIoRingRegisterFileHandles" />
    public void BuildRegisterFileHandles(
        ReadOnlySpan<HANDLE> handles,
        nint userData = 0)
    {
        BuildIoRingRegisterFileHandlesChecked(this, handles, userData);
    }

    /// <inheritdoc cref="SetIoRingCompletionEvent" />
    public void SetCompletionEvent(HANDLE waitHandle)
    {
        SetIoRingCompletionEventChecked(this, waitHandle);
    }

    /// <inheritdoc cref="SetIoRingCompletionEvent" />
    public void SetCompletionEvent(EventWaitHandle waitHandle)
    {
        SetIoRingCompletionEventChecked(this, waitHandle.SafeWaitHandle.DangerousGetHandle());
    }

    /// <inheritdoc cref="CloseIoRing" />
    public void Close()
    {
        if (Handle.TryZeroHandle(out var ptr))
        {
            CloseIoRingChecked(ptr);
        }
    }

    public void Dispose()
    {
        Close();
    }
}
