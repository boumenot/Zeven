using System.Runtime.InteropServices;
using Zeven.Core.Interop;

namespace Zeven.Core;

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
public delegate int CreateObjectFunc(in Guid clsID, in Guid iid, out nint outObject);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
public delegate int GetNumberOfFormatsFunc(out uint numFormats);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
public delegate int GetHandlerProperty2Func(uint index, uint propID, ref PropVariant value);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
public delegate int CreateEncoderFunc(uint index, in Guid iid, out nint outObject);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
public delegate int CreateDecoderFunc(uint index, in Guid iid, out nint outObject);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
public delegate int GetNumberOfMethodsFunc(out uint numMethods);

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
public delegate int GetMethodPropertyFunc(uint index, uint propID, ref PropVariant value);
