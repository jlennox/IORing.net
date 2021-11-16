#include <ntstatus.h>
#define WIN32_NO_STATUS
#include <Windows.h>
#include <cstdio>
#include <ioringapi.h>
#include <malloc.h>
#include <comdef.h>
#include <atlstr.h>

// This sample is mostly a copy from https://windows-internals.com/i-o-rings-when-one-i-o-operation-is-not-enough/
// The primary purpose of it being here is to allow in-memory structs and calls to be examined to ensure the C#
// implementations are correct.

inline CString GetMessageForHresult(HRESULT hr) {
    _com_error error(hr);
    CString cs;
    cs.Format(_T("Error 0x%08x: %s"), hr, error.ErrorMessage());
    return cs;
}

void IoRingKernelBase()
{
    HRESULT result;
    HIORING handle;
    IORING_CREATE_FLAGS flags;
    //IORING_HANDLE_REF requestDataFile;
    //IORING_BUFFER_REF requestDataBuffer;
    UINT32 submittedEntries;
    HANDLE hFile = NULL;
    ULONG sizeToRead = 0x200;
    PVOID* buffer = NULL;
    ULONG64 endOfBuffer;

    flags.Required = IORING_CREATE_REQUIRED_FLAGS_NONE;
    flags.Advisory = IORING_CREATE_ADVISORY_FLAGS_NONE;
    result = CreateIoRing(IORING_VERSION_1, flags, 1, 1, &handle);
    if (!SUCCEEDED(result))
    {
        printf("Failed creating IO ring handle: 0x%x\n", result);
        //goto Exit;
    }

    hFile = CreateFile(L"C:\\Windows\\System32\\notepad.exe",
        GENERIC_READ,
        0,
        NULL,
        OPEN_EXISTING,
        FILE_ATTRIBUTE_NORMAL,
        NULL);
    if (hFile == INVALID_HANDLE_VALUE)
    {
        printf("Failed opening file handle: 0x%x\n", GetLastError());
        //goto Exit;
    }
    //requestDataFile.Kind = IORING_REF_RAW;
    //requestDataFile.Handle = hFile;
    IORING_HANDLE_REF requestDataFile = IORING_HANDLE_REF(hFile);
    //requestDataBuffer.Kind = IORING_REF_RAW;
    buffer = (PVOID*)VirtualAlloc(NULL,
        sizeToRead,
        MEM_COMMIT,
        PAGE_READWRITE);
    IORING_BUFFER_REF requestDataBuffer = IORING_BUFFER_REF(buffer);
    if (buffer == NULL)
    {
        printf("Failed to allocate memory\n");
        goto Exit;
    }
    //requestDataBuffer.Buffer = buffer;
    result = BuildIoRingReadFile(handle,
        requestDataFile,
        requestDataBuffer,
        sizeToRead,
        0,
        NULL,
        IOSQE_FLAGS_NONE);
    if (!SUCCEEDED(result))
    {
        printf("Failed building IO ring read file structure: 0x%x\n", result);
        goto Exit;
    }

    result = SubmitIoRing(handle, 1, 10000, &submittedEntries);
    if (!SUCCEEDED(result))
    {
        printf("Failed submitting IO ring: 0x%x\n", result);
        goto Exit;
    }
    printf("Data from file:\n");
    endOfBuffer = (ULONG64)buffer + sizeToRead;
    for (; (ULONG64)buffer < endOfBuffer; buffer++)
    {
        printf("%p ", *buffer);
    }
    printf("\n");

Exit:
    if (handle != 0)
    {
        CloseIoRing(handle);
    }
    if (hFile)
    {
        CloseHandle(hFile);
    }
    if (buffer)
    {
        VirtualFree(buffer, NULL, MEM_RELEASE);
    }
}

int main()
{
    IoRingKernelBase();
    ExitProcess(0);
}