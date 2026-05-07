using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Zeven.Core.Interop;

namespace Zeven.Core;

/// <summary>
/// Shared marshaling logic for applying archive properties via ISetProperties.
/// </summary>
internal static class ArchiveOptions
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
                    values[i] = PropVariant.FromObject(props[i].Value);
                }

                int hr = setProps.SetProperties((nint)names, (nint)values, (uint)props.Count);

                for (int i = 0; i < props.Count; i++)
                {
                    Marshal.FreeBSTR(names[i]);
                    values[i].FreeBstr();
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
