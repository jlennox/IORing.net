using System;
using System.Runtime.InteropServices;

namespace IORing;

public enum HResultCode : ulong
{
    S_OK = 0,
    S_FALSE = 1,

    E_NOTIMPL = 0x80004001L,
    E_NOINTERFACE = 0x80004002L,
    E_POINTER = 0x80004003L,
    E_ABORT = 0x80004004L,
    E_FAIL = 0x80004005L,
    E_UNEXPECTED = 0x8000FFFFL,
    E_ACCESSDENIED = 0x80070005L,
    E_HANDLE = 0x80070006L,
    E_OUTOFMEMORY = 0x8007000EL,
    E_INVALIDARG = 0x80070057L,

    E_END_OF_FILE = 0x80070026L,

    /// <summary>
    /// MessageId: IORING_E_REQUIRED_FLAG_NOT_SUPPORTED
    /// One or more of the required flags provided is unknown by the implementation.
    /// </summary>
    IORING_E_REQUIRED_FLAG_NOT_SUPPORTED = 0x80460001L,

    /// <summary>
    /// MessageId: IORING_E_SUBMISSION_QUEUE_FULL
    /// The submission queue is full.
    /// </summary>
    IORING_E_SUBMISSION_QUEUE_FULL = 0x80460002L,

    /// <summary>
    /// MessageId: IORING_E_VERSION_NOT_SUPPORTED
    /// The version specified is not known or supported.
    /// </summary>
    IORING_E_VERSION_NOT_SUPPORTED = 0x80460003L,

    /// <summary>
    /// MessageId: IORING_E_SUBMISSION_QUEUE_TOO_BIG
    /// The submission queue size specified for the IoRing is too big.
    /// </summary>
    IORING_E_SUBMISSION_QUEUE_TOO_BIG = 0x80460004L,

    /// <summary>
    /// MessageId: IORING_E_COMPLETION_QUEUE_TOO_BIG
    /// The completion queue size specified for the IoRing is too big.
    /// </summary>
    IORING_E_COMPLETION_QUEUE_TOO_BIG = 0x80460005L,

    /// <summary>
    /// MessageId: IORING_E_SUBMIT_IN_PROGRESS
    /// A submit operation is already in progress for this IoRing on another thread.
    /// </summary>
    IORING_E_SUBMIT_IN_PROGRESS = 0x80460006L,

    /// <summary>
    /// MessageId: IORING_E_CORRUPT
    /// The shared ring buffers of the IoRing are corrupt.
    /// </summary>
    IORING_E_CORRUPT = 0x80460007L,
}

public static unsafe class KernelBase
{
    /// <summary>
    /// Creates a new instance of an I/O ring submission/completion queue pair and returns a handle for referencing the IORING.
    /// </summary>
    /// <param name="ioRingVersion">A UNIT32 representing the version of the I/O ring API the ring is created for. This value must be less than or equal to the value retrieved from a call to QueryIoRingCapabilities</param>
    /// <param name="flags">A value from the IORING_CREATE_FLAGS enumeration specifying creation flags.</param>
    /// <param name="submissionQueueSize">The requested minimum submission queue size. The system may round up the size as needed to ensure the actual size is a power of 2. You can get the actual allocated queue size by calling GetIoRingInfo. You can get the maximum submission queue size on the current system by calling QueryIoRingCapabilities.</param>
    /// <param name="completionQueueSize">The requested minimum size of the completion queue. The system will round this size up to a power of two that is no less than two times the actual submission queue size to allow for submissions while some operations are still in progress. You can get the actual allocated queue size by calling GetIoRingInfo.</param>
    /// <param name="handle">Receives the resulting HIORING handle, if creation was successful. The returned HIORING ring must be closed by calling CloseIoRing, not CloseHandle, to release the underlying resources for the IORING.</param>
    /// <returns>An HRESULT, including but not limited to the following:
    /// S_OK Success.
    /// IORING_E_UNKNOWN_VERSION    The version specified in ioringVersion is unknown.
    /// </returns>
    [DllImport("KernelBase.dll", SetLastError = true)]
    public static extern HRESULT CreateIoRing(
        IORING_VERSION ioRingVersion,
        IORING_CREATE_FLAGS flags,
        uint submissionQueueSize,
        uint completionQueueSize,
        out HIORING handle);

    /// <inheritdoc cref="CreateIoRing" />
    public static HIORING CreateIoRingChecked(
        IORING_VERSION ioRingVersion,
        IORING_CREATE_FLAGS flags,
        uint submissionQueueSize,
        uint completionQueueSize)
    {
        CreateIoRing(ioRingVersion, flags, submissionQueueSize, completionQueueSize, out var handle)
            .Check(nameof(CreateIoRing));

        return handle;
    }

    /// <summary>
    /// Queries the OS for the supported capabilities for I/O rings.
    /// </summary>
    /// <param name="capabilities">Receives a pointer to an IORING_CAPABILITIES representing the I/O ring API capabilities.</param>
    /// <returns>S_OK on success.</returns>
    /// <remarks>The results of this call are internally cached per-process, so this is efficient to call multiple times as only the first will transition to the kernel to retrieve the data.Note that the results are not guaranteed to contain the same values between runs of the same process or even between processes on the same system. So applications should not store this information beyond the lifetime of the process and should not assume that other processes have the same support.</remarks>
    [DllImport("KernelBase.dll", SetLastError = true)]
    public static extern HRESULT QueryIoRingCapabilities(out IORING_CAPABILITIES capabilities);

    /// <inheritdoc cref="QueryIoRingCapabilities" />
    public static IORING_CAPABILITIES QueryIoRingCapabilitiesChecked()
    {
        QueryIoRingCapabilities(out var capabilities)
            .Check(nameof(QueryIoRingCapabilities));

        return capabilities;
    }

    /// <summary>
    /// Registers an array of file handles with the system for future I/O ring operations.
    /// </summary>
    /// <param name="ioRing">An HIORING representing a handle to the I/O ring for which file handles are registered.</param>
    /// <param name="count">A UINT32 specifying the number of handles provided in the handles parameter.</param>
    /// <param name="handles">An array of HANDLE values to be registered.</param>
    /// <param name="userData">A UINT_PTR value identifying the registration operation. Specify this value when cancelling the operation with a call to BuildIoRingCancelRequest. If an app implements cancellation behavior for the operation, the userData value must be unique. Otherwise, the value is treated as opaque by the system and can be anything, including 0.</param>
    /// <returns>
    /// Returns an HRESULT including, but not limited to the following:
    /// S_OK Success
    /// IORING_E_SUBMISSION_QUEUE_FULL The submission queue is full, and no additional entries are available to build.The application must submit the existing entries and wait for some of them to complete before adding more operations to the queue.
    /// IORING_E_UNKNOWN_REQUIRED_FLAG The application provided a required flag that is not known to the implementation.Library code should check the IoRingVersion field of the IORING_INFO obtained from a call to GetIoRingInfo to determine the API version of an I/O ring which determines the operations and flags that are supported.Applications should know the version they used to create the I/O ring and therefore should not provide unsupported flags at runtime.
    /// </returns>
    /// <remarks>This function allows the kernel implementation to perform the validation and internal mapping just once avoiding the overhead on each I/O operation. Subsequent entries in the submission queue may refer to the handles registered with this function using an integer index into the array. If a previous registration exists, this replaces the previous registration completely.</remarks>
    [DllImport("KernelBase.dll", SetLastError = true)]
    public static extern HRESULT BuildIoRingRegisterFileHandles(
        HIORING ioRing,
        uint count,
        HANDLE* handles,
        nint userData);

    /// <inheritdoc cref="BuildIoRingRegisterFileHandles" />
    public static void BuildIoRingRegisterFileHandlesChecked(
        HIORING ioRing,
        ReadOnlySpan<HANDLE> handles,
        nint userData = 0)
    {
        fixed (HANDLE* handlesPtr = handles)
        {
            checked
            {
                BuildIoRingRegisterFileHandles(ioRing, (uint)handles.Length, handlesPtr, userData)
                    .Check(nameof(BuildIoRingRegisterFileHandles));
            }
        }
    }

    /// <summary>
    /// Registers an array of buffers with the system for future I/O ring operations.
    /// </summary>
    /// <param name="ioRing">An HIORING representing a handle to the I/O ring for which buffers are registered.</param>
    /// <param name="count">A UINT32 specifying the number of buffers provided in the buffers parameter.</param>
    /// <param name="buffers">An array of IORING_BUFFER_INFO structures representing the buffers to be registered.</param>
    /// <param name="userData">A UINT_PTR value identifying the registration operation. Specify this value when cancelling the operation with a call to BuildIoRingCancelRequest. If an app implements cancellation behavior for the operation, the userData value must be unique. Otherwise, the value is treated as opaque by the system and can be anything, including 0.</param>
    /// <returns>
    /// Returns an HRESULT including, but not limited to the following:
    /// S_OK Success
    /// IORING_E_SUBMISSION_QUEUE_FULL The submission queue is full, and no additional entries are available to build.The application must submit the existing entries and wait for some of them to complete before adding more operations to the queue.
    /// IORING_E_UNKNOWN_REQUIRED_FLAG The application provided a required flag that is not known to the implementation.Library code should check the IoRingVersion field of the IORING_INFO obtained from a call to GetIoRingInfo to determine the API version of an I/O ring which determines the operations and flags that are supported.Applications should know the version they used to create the I/O ring and therefore should not provide unsupported flags at runtime.
    /// </returns>
    /// <remarks>This function allows the kernel implementation to perform the validation and internal mapping just once avoiding the overhead on each I/O operation. Subsequent entries in the submission queue may refer to the buffers registered with this function using an integer index into the array. If a previous registration exists, this replaces the previous registration completely. Any entries in the array with an Address of NULL and a Length of 0 are sparse entries, and not used. This allows you to release one or more of the previously registered buffers.</remarks>
    [DllImport("KernelBase.dll", SetLastError = true)]
    public static extern HRESULT BuildIoRingRegisterBuffers(
        HIORING ioRing,
        uint count,
        IORING_BUFFER_INFO* buffers,
        nint userData);

    /// <inheritdoc cref="BuildIoRingRegisterBuffers" />
    public static void BuildIoRingRegisterBuffersChecked(
        HIORING ioRing,
        ReadOnlySpan<IORING_BUFFER_INFO> buffers,
        nint userData = 0)
    {
        fixed (IORING_BUFFER_INFO* buffersPtr = buffers)
        {
            checked
            {
                BuildIoRingRegisterBuffers(ioRing, (uint)buffers.Length, buffersPtr, userData)
                    .Check(nameof(BuildIoRingRegisterBuffers));
            }
        }
    }

    /// <summary>
    /// Performs an asynchronous read from a file using an I/O ring. This operation is similar to calling ReadFileEx.
    /// </summary>
    /// <param name="ioRing">An HIORING representing a handle to the I/O ring which will perform the read operation.</param>
    /// <param name="fileRef">An IORING_HANDLE_REF specifying the file to read.</param>
    /// <param name="dataRef">An IORING_BUFFER_REF specifying the buffer into which the file is read. The provided buffer must have a size of at least numberOfBytesToRead bytes.</param>
    /// <param name="numberOfBytesToRead">The number of bytes to read.</param>
    /// <param name="fileOffset">The offset into the file to begin reading.</param>
    /// <param name="userData">A UINT_PTR value identifying the file read operation. Specify this value when cancelling the operation with a call to BuildIoRingCancelRequest. If an app implements cancellation behavior for the operation, the userData value must be unique. Otherwise, the value is treated as opaque by the system and can be anything, including 0.</param>
    /// <param name="flags">A bitwise OR combination of values from the IORING_SQE_FLAGS enumeration specifying kernel behavior options for I/O ring submission queue entries.</param>
    /// <returns>
    /// Returns an HRESULT including, but not limited to the following:
    /// S_OK Success
    /// IORING_E_SUBMISSION_QUEUE_FULL The submission queue is full, and no additional entries are available to build.The application must submit the existing entries and wait for some of them to complete before adding more operations to the queue.
    /// IORING_E_UNKNOWN_REQUIRED_FLAG The application provided a required flag that is not known to the implementation.Library code should check the IoRingVersion field of the IORING_INFO obtained from a call to GetIoRingInfo to determine the API version of an I/O ring which determines the operations and flags that are supported.Applications should know the version they used to create the I/O ring and therefore should not provide unsupported flags at runtime.
    /// </returns>
    /// <remarks>Check I/O ring support for read file operations by calling IsIoRingOpSupported and specifying IORING_OP_READ for the op parameter.</remarks>
    [DllImport("KernelBase.dll", SetLastError = true)]
    public static extern HRESULT BuildIoRingReadFile(
        HIORING ioRing,
        IORING_HANDLE_REF fileRef,
        IORING_BUFFER_REF dataRef,
        uint numberOfBytesToRead,
        ulong fileOffset,
        nint userData,
        IORING_SQE_FLAGS flags);

    /// <inheritdoc cref="BuildIoRingReadFile" />
    public static void BuildIoRingReadFileChecked(
        HIORING ioRing,
        IORING_HANDLE_REF fileRef,
        IORING_BUFFER_REF dataRef,
        uint numberOfBytesToRead,
        ulong fileOffset,
        nint userData = 0,
        IORING_SQE_FLAGS flags = IORING_SQE_FLAGS.NONE)
    {
        BuildIoRingReadFile(ioRing, fileRef, dataRef, numberOfBytesToRead, fileOffset, userData, flags)
            .Check(nameof(BuildIoRingReadFile));
    }

    /// <inheritdoc cref="BuildIoRingRegisterBuffers" />
    /// <returns>False is timeout was reached, true otherwise (NOT YET SUPPORTED).</returns>
    public static bool TryBuildIoRingReadFileChecked(
        HIORING ioRing,
        IORING_HANDLE_REF fileRef,
        IORING_BUFFER_REF dataRef,
        uint numberOfBytesToRead,
        ulong fileOffset,
        nint userData = 0,
        IORING_SQE_FLAGS flags = IORING_SQE_FLAGS.NONE)
    {
        var result = BuildIoRingReadFile(ioRing, fileRef, dataRef, numberOfBytesToRead, fileOffset, userData, flags);

        // TODO: The document mentions this code but it's not yet defined in the headers.
        // if (result.Code == HResultCode.IORING_E_WAIT_TIMEOUT)
        // {
        //     return false;
        // }

        result.Check(nameof(BuildIoRingReadFile));
        return true;
    }

    /// <summary>
    /// Submits all constructed but not yet submitted entries to the kernel’s queue and optionally waits for a set of operations to complete.
    /// </summary>
    /// <param name="ioRingHandle">An HIORING representing a handle to the I/O ring for which entries will be submitted.</param>
    /// <param name="waitOperations">The number of completion queue entries to wait for. Specifying 0 indicates that the call should not wait. This value must be less than the sum of the number of entries in the submission queue and the number of operations currently in progress.</param>
    /// <param name="milliseconds">The number of milliseconds to wait for the operations to complete. Specify INFINITE to wait indefinitely. This value is ignored if 0 is specified for waitOperations.</param>
    /// <param name="submittedEntries">Optional. Receives a pointer to an array of UINT_32 values representing the number of entries submitted.</param>
    /// <returns>
    /// Returns an HRESULT including, but not limited to, one of the following:
    /// S_OK All entries in the queue were submitted without error.
    /// IORING_E_WAIT_TIMEOUT   All operations were submitted without error and the subsequent wait timed out.
    /// Any other error value   Failure to process the submission queue in its entirety.
    /// </returns>
    /// <remarks>If this function returns an error other than IORING_E_WAIT_TIMEOUT, then all entries remain in the submission queue. Any errors processing a single submission queue entry results in a synchronous completion of that entry posted to the completion queue with an error status code for that operation.</remarks>
    [DllImport("KernelBase.dll", SetLastError = true)]
    public static extern HRESULT SubmitIoRing(
        HIORING ioRingHandle,
        uint waitOperations,
        uint milliseconds,
        out uint submittedEntries);

    /// <inheritdoc cref="SubmitIoRing" />
    /// <param name="timeout">The amount of time to wait for waitOperations to complete. Ignored if waitOperations is 0.</param>
    public static uint SubmitIoRingChecked(
        HIORING ioRingHandle,
        uint waitOperations,
        TimeSpan timeout)
    {
        SubmitIoRing(ioRingHandle, waitOperations, (uint)timeout.TotalMilliseconds, out var submittedEntries)
            .Check(nameof(SubmitIoRing));

        return submittedEntries;
    }

    /// <inheritdoc cref="SubmitIoRing" />
    public static uint SubmitIoRingChecked(HIORING ioRingHandle)
    {
        SubmitIoRing(ioRingHandle, 0, default, out var submittedEntries)
            .Check(nameof(SubmitIoRing));

        return submittedEntries;
    }

    /// <summary>
    /// Gets information about the API version and queue sizes of an I/O ring.
    /// </summary>
    /// <param name="ioRingHandle">An HIORING representing a handle to the I/O ring for which information is being queried.</param>
    /// <param name="ioRingBasicInfo">Receives a pointer to an IORING_INFO structure specifying API version and queue sizes for the specified I/O ring.</param>
    /// <returns>S_OK on success.</returns>
    [DllImport("KernelBase.dll", SetLastError = true)]
    public static extern HRESULT GetIoRingInfo(HIORING ioRingHandle, out IORING_INFO ioRingBasicInfo);

    /// <inheritdoc cref="GetIoRingInfo" />
    public static IORING_INFO GetIoRingInfoChecked(HIORING ioRingHandle)
    {
        GetIoRingInfo(ioRingHandle, out var ioRingBasicInfo)
            .Check(nameof(GetIoRingInfo));

        return ioRingBasicInfo;
    }

    /// <summary>
    /// Closes an HIORING handle that was previously opened with a call to CreateIoRing.
    /// </summary>
    /// <param name="ioRingHandle">The HIORING handle to close.</param>
    /// <returns>Returns S_OK on success.</returns>
    /// <remarks>Calling this function ensures that resources allocated for the I/O ring are released. The closed handle is no longer valid after the function returns. It is important to note that closing the handle tosses the operations that are queued but not submitted. However, the operations that are in flight are not cancelled.
    /// It is possible that reads from or writes to memory buffers may still occur after CloseIoRing returns.If you want to ensure that no pending reads or writes occur, you must wait for the completions to appear in the completion queue for all the operations that are submitted.You may choose to cancel the previously submitted operations before waiting on their completions.As an alternative to submitting multiple cancel requests, you can call CancelIoEx with the file handle and NULL for the overlapped pointer to effectively cancel all pending operations on the handle.</remarks>
    [DllImport("KernelBase.dll", SetLastError = true)]
    public static extern HRESULT CloseIoRing(HIORING ioRingHandle);

    /// <inheritdoc cref="CloseIoRing" />
    public static void CloseIoRingChecked(HIORING ioRingHandle)
    {
        CloseIoRing(ioRingHandle)
            .Check(nameof(CloseIoRing));
    }

    /// <summary>
    /// Queries the support of the specified operation for the specified I/O ring.
    /// </summary>
    /// <param name="ioRing">An HIORING representing a handle to the I/O ring for which operation support is queried.</param>
    /// <param name="op">A value from the IORING_OP_CODE enumeration specifying the operation for which support is queried.</param>
    /// <returns>
    /// Returns an HRESULT including, but not limitted to the following:
    /// S_OK The operation is supported.
    /// S_FALSE The operation is unsupported.
    /// </returns>
    /// <remarks>Unknown operation codes are treated as unsupported. Invalid HIORING handles are treated as not supporting any operations. So, this method will not throw errors due to these conditions.</remarks>
    [DllImport("KernelBase.dll", SetLastError = true)]
    public static extern HRESULT IsIoRingOpSupported(HIORING ioRing, IORING_OP_CODE op);

    /// <inheritdoc cref="IsIoRingOpSupported" />
    public static bool IsIoRingOpSupportedChecked(HIORING ioRing, IORING_OP_CODE op)
    {
        return IsIoRingOpSupported(ioRing, op).Code == HResultCode.S_OK;
    }

    /// <summary>
    /// Pops a single entry from the completion queue, if one is available.
    /// </summary>
    /// <param name="ioRing">An HIORING representing a handle to the I/O ring from which an entry from the completion queue is popped.</param>
    /// <param name="cqe">Receives a pointer to an IORING_CQE structure representing the completed queue entry.</param>
    /// <returns>
    /// Returns an HRESULT including, but not limitted to the following:
    /// S_OK The entry was popped from the queue and the IORING_CQE pointed to by cqe contains the values from the entry.
    /// S_FALSE The completion queue is empty, and the data pointed to by the cqe parameter is unmodified.
    /// </returns>
    [DllImport("KernelBase.dll", SetLastError = true)]
    public static extern HRESULT PopIoRingCompletion(HIORING ioRing, out IORING_CQE cqe);

    /// <inheritdoc cref="PopIoRingCompletion" />
    public static bool TryPopIoRingCompletionChecked(HIORING ioRing, out IORING_CQE cqe)
    {
        var result = PopIoRingCompletion(ioRing, out cqe);
        // No completions are available.
        if (result.Code == HResultCode.S_FALSE) return false;

        result.Check(nameof(PopIoRingCompletion));

        // Arg. This is really silly.
        if (!cqe.IsEndOfFile())
        {
            cqe.Check(nameof(IORING_CQE.ResultCode));
        }

        return true;
    }

    // TODO: This is not yet documented on MSDN.
    [DllImport("KernelBase.dll", SetLastError = true)]
    public static extern HRESULT SetIoRingCompletionEvent(HIORING ioRing, HANDLE hEvent);

    /// <inheritdoc cref="SetIoRingCompletionEvent" />
    public static void SetIoRingCompletionEventChecked(HIORING ioRing, HANDLE hEvent)
    {
        SetIoRingCompletionEvent(ioRing, hEvent)
            .Check(nameof(SetIoRingCompletionEvent));
    }

    /// <summary>
    /// Attempts to cancel a previously submitted I/O ring operation.
    /// </summary>
    /// <param name="ioRing">An HIORING representing a handle to the I/O ring for which a cancellation is requested.</param>
    /// <param name="file">An IORING_HANDLE_REF representing the file associated with the operation to cancel.</param>
    /// <param name="opToCancel">A UINT_PTR specifying the operation to cancel. This value is the same value provided in the userData parameter when the operation was registered. To support cancellation, the userData value must be unique for each operation.</param>
    /// <param name="userData">A UINT_PTR value identifying the cancellation operation. Specify this value when cancelling the operation with a call to BuildIoRingCancelRequest. If an app implements cancellation behavior for the operation, the userData value must be unique. Otherwise, the value is treated as opaque by the system and can be anything, including 0.</param>
    /// <returns>
    /// S_OK 	Success
    /// IORING_E_SUBMISSION_QUEUE_FULL The submission queue is full, and no additional entries are available to build.The application must submit the existing entries and wait for some of them to complete before adding more operations to the queue.
    /// IORING_E_UNKNOWN_REQUIRED_FLAG The application provided a required flag that is not known to the implementation.Library code should check the IoRingVersion field of the IORING_INFO obtained from a call to GetIoRingInfo to determine the API version of an I/O ring which determines the operations and flags that are supported.Applications should know the version they used to create the I/O ring and therefore should not provide unsupported flags at runtime.
    /// </returns>
    [DllImport("KernelBase.dll", SetLastError = true)]
    public static extern HRESULT BuildIoRingCancelRequest(HIORING ioRing, IORING_HANDLE_REF file, nint opToCancel, nint userData);

    /// <inheritdoc cref="BuildIoRingCancelRequest" />
    public static void BuildIoRingCancelRequestChecked(HIORING ioRing, IORING_HANDLE_REF file, nint opToCancel, nint userData)
    {
        BuildIoRingCancelRequest(ioRing, file, opToCancel, userData)
            .Check(nameof(BuildIoRingCancelRequest));
    }
}
