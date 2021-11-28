# IORing.net

A library for using Windows I/O Ring from C#/.net.

## Support
I/O Ring version 1 is supported, and at this time of writing, is the only public
release.

As of version 1, only file reading is supported.

## Convention
The `KernelBase` class has direct calls to the I/O Ring APIs. There's also
more C# friendly overloads that are either postfixed with `Checked` or prefixed
with `Try`. Further, there's overloads on `HIORING` which remove the "IoRing"
from their names.

For example, `BuildIoRingRegisterFileHandles` is a direct API call. It takes
a pointer to a HANDLE array, and the count of entries in said array. It returns
an HRESULT which the caller must check.

`BuildIoRingRegisterFileHandlesChecked` takes a `ReadOnlySpan<HANDLE>` and
returns void. An exception is thrown if an error representing HRESULT is
returned.

`HIORING.BuildRegisterFileHandles` also exists to provide a more OOP pattern.

## Examples

### [IORing.Samples.ReadFie](src/IORing.Samples.Grep/Grep.cs)
* A multi-threaded "grep" like searching utility. Regex is not supported.

### [IORing.Samples.ReadFie](src/IORing.Samples.ReadFile/Program.cs)
* Registers multiple buffers.
* Adds multiple read operations into registered buffers.
* Properly avoids usage of wait handle when not strictly needed.

## Resources
* https://windows-internals.com/i-o-rings-when-one-i-o-operation-is-not-enough/
* https://docs.microsoft.com/en-us/windows/win32/api/ioringapi/