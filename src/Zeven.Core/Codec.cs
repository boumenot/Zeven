using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Zeven.Core.Interop;

#pragma warning disable CS9191 // Marshal.QueryInterface takes ref Guid, not in Guid

namespace Zeven.Core;

/// <summary>
/// Shared codec helpers for 7-Zip compression via ZevenFormat.
/// Used by Lzma2Codec, PpmdCodec, etc.
/// </summary>
internal static class Codec
{
    private static void CodeStreams(ICompressCoder coder, StrategyBasedComWrappers cw,
        Stream input, Stream output, long outSize = -1)
    {
        var inWrapper = new InStreamWrapper(input);
        var outWrapper = new OutStreamWrapper(output);

        nint inCcw = cw.GetOrCreateComInterfaceForObject(inWrapper, CreateComInterfaceFlags.None);
        Guid iidSeqIn = Iid.ISequentialInStream;
        Marshal.QueryInterface(inCcw, ref iidSeqIn, out nint inPtr);

        nint outCcw = cw.GetOrCreateComInterfaceForObject(outWrapper, CreateComInterfaceFlags.None);
        Guid iidSeqOut = Iid.ISequentialOutStream;
        Marshal.QueryInterface(outCcw, ref iidSeqOut, out nint outPtr);

        try
        {
            int hr;
            unsafe
            {
                if (outSize >= 0)
                {
                    ulong size = (ulong)outSize;
                    hr = coder.Code(inPtr, outPtr, nint.Zero, (nint)(&size), nint.Zero);
                }
                else
                {
                    hr = coder.Code(inPtr, outPtr, nint.Zero, nint.Zero, nint.Zero);
                }
            }

            Marshal.ThrowExceptionForHR(hr);
        }
        finally
        {
            if (inPtr != nint.Zero) { Marshal.Release(inPtr); }
            if (outPtr != nint.Zero) { Marshal.Release(outPtr); }
            Marshal.Release(inCcw);
            Marshal.Release(outCcw);
            GC.KeepAlive(inWrapper);
            GC.KeepAlive(outWrapper);
        }
    }

    private static void ApplyProperties(ICodecOptions options, ICompressSetCoderProperties setProps)
    {
        var props = options.GetProperties();
        if (props.Count == 0)
        {
            return;
        }

        unsafe
        {
            var propIds = stackalloc uint[props.Count];
            var propVals = stackalloc PropVariant[props.Count];

            int allocatedCount = 0;
            try
            {
                foreach (var (propId, value) in props)
                {
                    propIds[allocatedCount] = propId;
                    propVals[allocatedCount] = PropVariant.FromObject(value);
                    allocatedCount++;
                }

                int hr = setProps.SetCoderProperties((nint)propIds, (nint)propVals, (uint)props.Count);
                Marshal.ThrowExceptionForHR(hr);
            }
            finally
            {
                for (int j = 0; j < allocatedCount; j++)
                {
                    propVals[j].FreeBstr();
                }
            }
        }
    }

    /// <summary>
    /// Creates an encoder, sets properties, captures the property header bytes, then releases.
    /// </summary>
    internal static byte[] CapturePropertyHeader(ICodecOptions options)
    {
        ulong codecId = options.CodecId;
        var lib = ZevenLibrary.Instance;
        int codecIndex = lib.FindCodecIndex(codecId);
        if (codecIndex < 0)
        {
            throw new InvalidOperationException($"Codec {ZevenFormat.CodecName(codecId)} not found in 7z.dll");
        }

        nint encoderPtr = lib.CreateEncoderObject((uint)codecIndex);
        var cw = lib.ComWrappers;

        try
        {
            // Set compression properties
            Guid iidSetProps = Iid.ICompressSetCoderProperties;
            Marshal.QueryInterface(encoderPtr, ref iidSetProps, out nint setPropsPtr);
            if (setPropsPtr != nint.Zero)
            {
                try
                {
                    var setProps = (ICompressSetCoderProperties)cw.GetOrCreateObjectForComInstance(
                        setPropsPtr, CreateObjectFlags.UniqueInstance);
                    ApplyProperties(options, setProps);
                }
                finally
                {
                    Marshal.Release(setPropsPtr);
                }
            }

            // Capture property header bytes
            Guid iidWriteProps = Iid.ICompressWriteCoderProperties;
            Marshal.QueryInterface(encoderPtr, ref iidWriteProps, out nint writePropsPtr);
            if (writePropsPtr != nint.Zero)
            {
                try
                {
                    var writeProps = (ICompressWriteCoderProperties)cw.GetOrCreateObjectForComInstance(
                        writePropsPtr, CreateObjectFlags.UniqueInstance);
                    using var propStream = new MemoryStream();
                    var outWrapper = new OutStreamWrapper(propStream);
                    nint outCcw = cw.GetOrCreateComInterfaceForObject(
                        outWrapper, CreateComInterfaceFlags.None);
                    Guid iidSeqOut = Iid.ISequentialOutStream;
                    Marshal.QueryInterface(outCcw, ref iidSeqOut, out nint outPtr);

                    try
                    {
                        int hr = writeProps.WriteCoderProperties(outPtr);
                        Marshal.ThrowExceptionForHR(hr);
                    }
                    finally
                    {
                        if (outPtr != nint.Zero) { Marshal.Release(outPtr); }
                        Marshal.Release(outCcw);
                        GC.KeepAlive(outWrapper);
                    }

                    return propStream.ToArray();
                }
                finally
                {
                    Marshal.Release(writePropsPtr);
                }
            }

            return [];
        }
        finally
        {
            Marshal.Release(encoderPtr);
        }
    }

    /// <summary>
    /// Creates a fresh encoder, compresses raw bytes from input to output, then releases.
    /// Does not write any framing — just raw compressed data.
    /// </summary>
    internal static void CompressBlock(ICodecOptions options, byte[] propertyHeader,
            Stream input, Stream output)
    {
        ulong codecId = options.CodecId;
        var lib = ZevenLibrary.Instance;
        int codecIndex = lib.FindCodecIndex(codecId);
        if (codecIndex < 0)
        {
            throw new InvalidOperationException($"Codec {ZevenFormat.CodecName(codecId)} not found in 7z.dll");
        }

        nint encoderPtr = lib.CreateEncoderObject((uint)codecIndex);
        var cw = lib.ComWrappers;

        try
        {
            // Set compression properties
            Guid iidSetProps = Iid.ICompressSetCoderProperties;
            Marshal.QueryInterface(encoderPtr, ref iidSetProps, out nint setPropsPtr);
            if (setPropsPtr != nint.Zero)
            {
                try
                {
                    var setProps = (ICompressSetCoderProperties)cw.GetOrCreateObjectForComInstance(
                        setPropsPtr, CreateObjectFlags.UniqueInstance);
                    ApplyProperties(options, setProps);
                }
                finally
                {
                    Marshal.Release(setPropsPtr);
                }
            }

            // Encode
            var coder = (ICompressCoder)cw.GetOrCreateObjectForComInstance(
                encoderPtr, CreateObjectFlags.UniqueInstance);
            CodeStreams(coder, cw, input, output);
        }
        finally
        {
            Marshal.Release(encoderPtr);
        }
    }

    /// <summary>
    /// Creates a fresh decoder, decompresses raw bytes from input to output, then releases.
    /// Does not read any framing — just raw compressed data.
    /// </summary>
    internal static void DecompressBlock(byte[] propertyHeader, ulong codecId,
            Stream input, Stream output, long outSize)
    {
        var lib = ZevenLibrary.Instance;
        int codecIndex = lib.FindCodecIndex(codecId);
        if (codecIndex < 0)
        {
            throw new InvalidOperationException($"Codec {ZevenFormat.CodecName(codecId)} not found in 7z.dll");
        }

        nint decoderPtr = lib.CreateDecoderObject((uint)codecIndex);
        var cw = lib.ComWrappers;

        try
        {
            // Set decoder properties
            Guid iidSetDecProps = Iid.ICompressSetDecoderProperties2;
            Marshal.QueryInterface(decoderPtr, ref iidSetDecProps, out nint setDecPropsPtr);
            if (setDecPropsPtr != nint.Zero)
            {
                try
                {
                    var setDecProps = (ICompressSetDecoderProperties2)cw.GetOrCreateObjectForComInstance(
                        setDecPropsPtr, CreateObjectFlags.UniqueInstance);
                    unsafe
                    {
                        fixed (byte* pProps = propertyHeader)
                        {
                            int hr = setDecProps.SetDecoderProperties2(
                                (nint)pProps, (uint)propertyHeader.Length);
                            Marshal.ThrowExceptionForHR(hr);
                        }
                    }
                }
                finally
                {
                    Marshal.Release(setDecPropsPtr);
                }
            }

            // Decode
            var coder = (ICompressCoder)cw.GetOrCreateObjectForComInstance(
                decoderPtr, CreateObjectFlags.UniqueInstance);
            CodeStreams(coder, cw, input, output, outSize);
        }
        finally
        {
            Marshal.Release(decoderPtr);
        }
    }
}
