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
        GC.SuppressFinalize(this);

        if (Handle.TryZeroHandle(out var ptr))
        {
            CloseIoRingChecked(ptr);
        }
    }
}

public unsafe class IORingBuffer : IDisposable
{
    public IntPtr Address => _address;
    public uint Length { init; get; }

    private IntPtr _address;

    public static implicit operator IORING_BUFFER_INFO(IORingBuffer d) => d.AsBufferInfo();
    public static implicit operator IORING_BUFFER_REF(IORingBuffer d) => d.AsBufferRef();

    public IORingBuffer(uint length, bool zeroInitialize = false)
    {
        checked
        {
            _address = Marshal.AllocHGlobal((int)length);
            Length = length;
        }

        if (zeroInitialize)
        {
            AsSpan().Fill(0);
        }
    }

    public IORING_BUFFER_INFO AsBufferInfo() => new(Address, Length);
    public IORING_BUFFER_REF AsBufferRef() => new(Address);

    public Span<byte> AsSpan() => AsSpan((int)Length);
    public Span<byte> AsSpan(int length) => new Span<byte>((byte*)Address, length);
    public ReadOnlySpan<byte> AsReadOnlySpan() => AsReadOnlySpan((int)Length);
    public ReadOnlySpan<byte> AsReadOnlySpan(int length) => new ReadOnlySpan<byte>((byte*)Address, length);

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        var pointer = Interlocked.Exchange(ref _address, IntPtr.Zero);
        if (pointer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(pointer);
        }
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

    /// <inheritdoc cref="CreateIoRing" />
    public static HIORING Create(
        IORING_VERSION ioRingVersion,
        IORING_CREATE_FLAGS flags,
        int submissionQueueSize,
        int completionQueueSize)
    {
        if (submissionQueueSize < 0) throw new ArgumentOutOfRangeException(nameof(submissionQueueSize), "Value must be positive.");
        if (completionQueueSize < 0) throw new ArgumentOutOfRangeException(nameof(submissionQueueSize), "Value must be positive.");

        return CreateIoRingChecked(ioRingVersion, flags, (uint)submissionQueueSize, (uint)completionQueueSize);
    }

    /// <inheritdoc cref="CreateIoRing" />
    public static HIORING Create(
        IORING_VERSION ioRingVersion,
        int submissionQueueSize,
        int completionQueueSize)
    {
        if (submissionQueueSize < 0) throw new ArgumentOutOfRangeException(nameof(submissionQueueSize), "Value must be positive.");
        if (completionQueueSize < 0) throw new ArgumentOutOfRangeException(nameof(submissionQueueSize), "Value must be positive.");

        return CreateIoRingChecked(ioRingVersion, new IORING_CREATE_FLAGS(), (uint)submissionQueueSize, (uint)completionQueueSize);
    }

    /// <inheritdoc cref="QueryIoRingCapabilities" />
    public static IORING_CAPABILITIES QueryCapabilities()
    {
        return QueryIoRingCapabilitiesChecked();
    }

    /// <inheritdoc cref="SubmitIoRing" />
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
        int numberOfBytesToRead,
        long offset,
        nint userData = 0,
        IORING_SQE_FLAGS flags = IORING_SQE_FLAGS.NONE)
    {
        if (numberOfBytesToRead < 0) throw new ArgumentOutOfRangeException(nameof(numberOfBytesToRead), "Value must be positive.");
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "Value must be positive.");

        BuildIoRingReadFileChecked(this, fileRef, dataRef, (uint)numberOfBytesToRead, (ulong)offset, userData, flags);
    }

    /// <inheritdoc cref="BuildIoRingReadFile" />
    public void BuildReadFile(
        FileStream filestream,
        IORING_BUFFER_REF dataRef,
        int numberOfBytesToRead,
        long offset,
        nint userData = 0,
        IORING_SQE_FLAGS flags = IORING_SQE_FLAGS.NONE)
    {
        if (numberOfBytesToRead < 0) throw new ArgumentOutOfRangeException(nameof(numberOfBytesToRead), "Value must be positive.");
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "Value must be positive.");

        var fileRef = new IORING_HANDLE_REF(filestream.SafeFileHandle.DangerousGetHandle());
        BuildIoRingReadFileChecked(this, fileRef, dataRef, (uint)numberOfBytesToRead, (ulong)offset, userData, flags);
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
