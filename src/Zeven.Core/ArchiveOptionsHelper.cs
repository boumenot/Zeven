using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Zeven.Core.Interop;

namespace Zeven.Core;

/// <summary>
/// Shared marshaling logic for applying archive properties via ISetProperties.
/// </summary>
internal static class ArchiveOptionsHelper
{
    public static void ApplyProperties(nint archivePtr, StrategyBasedComWrappers cw,
            List<(string Name, object Value)> props)
    {
        if (props.Count == 0) { return; }

        Guid iidSetProps = Iid.ISetProperties;
        Marshal.QueryInterface(archivePtr, ref iidSetProps, out nint setPropsPtr);
        if (setPropsPtr == nint.Zero) { return; }

        try
        {
            var setProps = (ISetProperties)cw.GetOrCreateObjectForComInstance(
                setPropsPtr, CreateObjectFlags.UniqueInstance);

            unsafe
            {
                var names = stackalloc nint[props.Count];
                var values = stackalloc PropVariant[props.Count];

                for (int i = 0; i < props.Count; i++)
                {
                    names[i] = Marshal.StringToBSTR(props[i].Name);
                    values[i] = default;
                    switch (props[i].Value)
                    {
                        case uint u:
                            values[i].VarType = PropVariant.VT_UI4;
                            values[i].UIntValue = u;
                            break;
                        case string s:
                            values[i].VarType = PropVariant.VT_BSTR;
                            values[i].PointerValue = Marshal.StringToBSTR(s);
                            break;
                        case bool b:
                            values[i].VarType = PropVariant.VT_BOOL;
                            values[i].BoolValue = (short)(b ? -1 : 0);
                            break;
                        case ulong ul:
                            values[i].VarType = PropVariant.VT_UI8;
                            values[i].ULongValue = ul;
                            break;
                    }
                }

                int hr = setProps.SetProperties((nint)names, (nint)values, (uint)props.Count);

                for (int i = 0; i < props.Count; i++)
                {
                    Marshal.FreeBSTR(names[i]);
                    if (values[i].VarType == PropVariant.VT_BSTR && values[i].PointerValue != nint.Zero)
                    {
                        Marshal.FreeBSTR(values[i].PointerValue);
                    }
                }

                Marshal.ThrowExceptionForHR(hr);
            }
        }
        finally
        {
            Marshal.Release(setPropsPtr);
        }
    }
}
