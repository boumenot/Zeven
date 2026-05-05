using System.Runtime.InteropServices;
using SevenZipNet.Interop;

namespace SevenZipNet;

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int CreateObjectFunc(in Guid clsID, in Guid iid, out nint outObject);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetNumberOfFormatsFunc(out uint numFormats);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate int GetHandlerProperty2Func(uint index, uint propID, ref PropVariant value);
