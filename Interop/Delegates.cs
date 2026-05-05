using System.Runtime.InteropServices;
using SevenZipNet.Interop;

namespace SevenZipNet;

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
public delegate int CreateObjectFunc(in Guid clsID, in Guid iid, out nint outObject);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
public delegate int GetNumberOfFormatsFunc(out uint numFormats);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
public delegate int GetHandlerProperty2Func(uint index, uint propID, ref PropVariant value);
