using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Zeven.Core.Interop;

#pragma warning disable CS9191 // Marshal.QueryInterface takes ref Guid, not in Guid

namespace Zeven.Core;

/// <summary>
/// Shared batch compress/decompress logic for any 7-Zip codec.
/// Used by Lzma2Codec, PpmdCodec, etc.
/// </summary>
internal static class CodecHelper
{
    public static void Compress(ICodecOptions options, Stream input, Stream output,
        bool writeSizePrefix = true)
    {
        ulong codecId = options.CodecId;
        var lib = ZevenLibrary.Instance;
        int codecIndex = lib.FindCodecIndex(codecId);
        if (codecIndex < 0)
        {
            throw new InvalidOperationException($"Codec 0x{codecId:X} not found in 7z.dll");
        }

        // Write 8-byte size prefix if requested (batch mode knows total size)
        if (writeSizePrefix)
        {
            long inputLength = input.Length - input.Position;
            Span<byte> sizeBytes = stackalloc byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(sizeBytes, inputLength);
            output.Write(sizeBytes);
        }

        nint encoderPtr = lib.CreateEncoderObject((uint)codecIndex);
        var cw = lib.ComWrappers;

        // Set compression properties
        Guid iidSetProps = Iid.ICompressSetCoderProperties;
        Marshal.QueryInterface(encoderPtr, ref iidSetProps, out nint setPropsPtr);
        if (setPropsPtr != nint.Zero)
        {
            var setProps = (ICompressSetCoderProperties)cw.GetOrCreateObjectForComInstance(
                setPropsPtr, CreateObjectFlags.UniqueInstance);
            ApplyProperties(options, setProps);
            Marshal.Release(setPropsPtr);
        }

        // Write property header (size varies by codec)
        Guid iidWriteProps = Iid.ICompressWriteCoderProperties;
        Marshal.QueryInterface(encoderPtr, ref iidWriteProps, out nint writePropsPtr);
        if (writePropsPtr != nint.Zero)
        {
            var writeProps = (ICompressWriteCoderProperties)cw.GetOrCreateObjectForComInstance(
                writePropsPtr, CreateObjectFlags.UniqueInstance);
            var outWrapper = new OutStreamWrapper(output);
            nint outCcw = cw.GetOrCreateComInterfaceForObject(outWrapper, CreateComInterfaceFlags.None);
            Guid iidSeqOut = Iid.ISequentialOutStream;
            Marshal.QueryInterface(outCcw, ref iidSeqOut, out nint outPtr);

            writeProps.WriteCoderProperties(outPtr);

            if (outPtr != nint.Zero) { Marshal.Release(outPtr); }
            Marshal.Release(outCcw);
            Marshal.Release(writePropsPtr);
            GC.KeepAlive(outWrapper);
        }

        // Encode
        var coder = (ICompressCoder)cw.GetOrCreateObjectForComInstance(
            encoderPtr, CreateObjectFlags.UniqueInstance);
        CodeStreams(coder, cw, input, output);
    }

    public static void Decompress(ulong codecId, Stream input, Stream output)
    {
        int propertyHeaderSize = GetPropertyHeaderSize(codecId);
        // Read 8-byte size prefix
        Span<byte> sizeBytes = stackalloc byte[8];
        if (input.ReadAtLeast(sizeBytes, 8, throwOnEndOfStream: false) < 8)
        {
            throw new InvalidDataException("Unexpected end of stream reading size prefix");
        }
        long uncompressedSize = BinaryPrimitives.ReadInt64LittleEndian(sizeBytes);

        // Read property header
        var propBytes = new byte[propertyHeaderSize];
        int bytesRead = input.ReadAtLeast(propBytes, propertyHeaderSize, throwOnEndOfStream: false);
        if (bytesRead < propertyHeaderSize)
        {
            throw new InvalidDataException(
                $"Expected {propertyHeaderSize}-byte property header, got {bytesRead} bytes");
        }

        var lib = ZevenLibrary.Instance;
        int codecIndex = lib.FindCodecIndex(codecId);
        if (codecIndex < 0)
        {
            throw new InvalidOperationException($"Codec 0x{codecId:X} not found in 7z.dll");
        }

        nint decoderPtr = lib.CreateDecoderObject((uint)codecIndex);
        var cw = lib.ComWrappers;

        // Set decoder properties
        Guid iidSetDecProps = Iid.ICompressSetDecoderProperties2;
        Marshal.QueryInterface(decoderPtr, ref iidSetDecProps, out nint setDecPropsPtr);
        if (setDecPropsPtr != nint.Zero)
        {
            var setDecProps = (ICompressSetDecoderProperties2)cw.GetOrCreateObjectForComInstance(
                setDecPropsPtr, CreateObjectFlags.UniqueInstance);
            unsafe
            {
                fixed (byte* pProps = propBytes)
                {
                    setDecProps.SetDecoderProperties2((nint)pProps, (uint)propertyHeaderSize);
                }
            }
            Marshal.Release(setDecPropsPtr);
        }

        // Decode
        var coder = (ICompressCoder)cw.GetOrCreateObjectForComInstance(
            decoderPtr, CreateObjectFlags.UniqueInstance);
        CodeStreams(coder, cw, input, output, outSize: uncompressedSize);
    }

    /// <summary>
    /// Initialize a decoder in stream-mode for incremental reading.
    /// Returns the decoder's ISequentialInStream COM pointer for direct Read() calls.
    /// </summary>
    public static nint InitStreamDecoder(ulong codecId,
        Stream input, StrategyBasedComWrappers cw, List<object> liveObjects,
        bool hasSizePrefix = true)
    {
        int propertyHeaderSize = GetPropertyHeaderSize(codecId);
        long uncompressedSize = -1;
        if (hasSizePrefix)
        {
            Span<byte> sizeBytes = stackalloc byte[8];
            if (input.ReadAtLeast(sizeBytes, 8, throwOnEndOfStream: false) < 8)
            {
                throw new InvalidDataException("Unexpected end of stream reading size prefix");
            }
            uncompressedSize = BinaryPrimitives.ReadInt64LittleEndian(sizeBytes);
        }

        var propBytes = new byte[propertyHeaderSize];
        int bytesRead = input.ReadAtLeast(propBytes, propertyHeaderSize, throwOnEndOfStream: false);
        if (bytesRead < propertyHeaderSize)
        {
            throw new InvalidDataException(
                $"Expected {propertyHeaderSize}-byte property header, got {bytesRead} bytes");
        }

        var lib = ZevenLibrary.Instance;
        int codecIndex = lib.FindCodecIndex(codecId);
        if (codecIndex < 0)
        {
            throw new InvalidOperationException($"Codec 0x{codecId:X} not found in 7z.dll");
        }

        nint decoderPtr = lib.CreateDecoderObject((uint)codecIndex);

        // Set decoder properties
        Guid iidSetDecProps = Iid.ICompressSetDecoderProperties2;
        Marshal.QueryInterface(decoderPtr, ref iidSetDecProps, out nint setDecPropsPtr);
        if (setDecPropsPtr != nint.Zero)
        {
            var setDecProps = (ICompressSetDecoderProperties2)cw.GetOrCreateObjectForComInstance(
                setDecPropsPtr, CreateObjectFlags.UniqueInstance);
            unsafe
            {
                fixed (byte* pProps = propBytes)
                {
                    setDecProps.SetDecoderProperties2((nint)pProps, (uint)propertyHeaderSize);
                }
            }
            Marshal.Release(setDecPropsPtr);
        }

        // Set input stream
        var inWrapper = new InStreamWrapper(input);
        liveObjects.Add(inWrapper);
        nint inCcw = cw.GetOrCreateComInterfaceForObject(inWrapper, CreateComInterfaceFlags.None);
        Guid iidSeqIn = Iid.ISequentialInStream;
        Marshal.QueryInterface(inCcw, ref iidSeqIn, out nint inPtr);

        Guid iidSetInStream = Iid.ICompressSetInStream;
        Marshal.QueryInterface(decoderPtr, ref iidSetInStream, out nint setInStreamPtr);
        if (setInStreamPtr != nint.Zero)
        {
            var setIn = (ICompressSetInStream)cw.GetOrCreateObjectForComInstance(
                setInStreamPtr, CreateObjectFlags.UniqueInstance);
            setIn.SetInStream(inPtr);
            Marshal.Release(setInStreamPtr);
        }

        if (inPtr != nint.Zero) { Marshal.Release(inPtr); }
        Marshal.Release(inCcw);

        // Initialize for stream-mode decoding
        Guid iidSetOutSize = Iid.ICompressSetOutStreamSize;
        Marshal.QueryInterface(decoderPtr, ref iidSetOutSize, out nint setOutSizePtr);
        if (setOutSizePtr != nint.Zero)
        {
            var setOutSize = (ICompressSetOutStreamSize)cw.GetOrCreateObjectForComInstance(
                setOutSizePtr, CreateObjectFlags.UniqueInstance);
            unsafe
            {
                if (uncompressedSize >= 0)
                {
                    ulong size = (ulong)uncompressedSize;
                    setOutSize.SetOutStreamSize((nint)(&size));
                }
                else
                {
                    setOutSize.SetOutStreamSize(nint.Zero);
                }
            }
            Marshal.Release(setOutSizePtr);
        }

        // QI for ISequentialInStream on the decoder itself
        Marshal.QueryInterface(decoderPtr, ref iidSeqIn, out nint decoderInStreamPtr);
        return decoderInStreamPtr;
    }

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

        if (inPtr != nint.Zero) { Marshal.Release(inPtr); }
        if (outPtr != nint.Zero) { Marshal.Release(outPtr); }
        Marshal.Release(inCcw);
        Marshal.Release(outCcw);
        GC.KeepAlive(inWrapper);
        GC.KeepAlive(outWrapper);

        Marshal.ThrowExceptionForHR(hr);
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

            int i = 0;
            foreach (var (propId, value) in props)
            {
                propIds[i] = propId;
                propVals[i] = default;
                switch (value)
                {
                    case uint u:
                        propVals[i].VarType = PropVariant.VT_UI4;
                        propVals[i].UIntValue = u;
                        break;
                    case ulong ul:
                        propVals[i].VarType = PropVariant.VT_UI8;
                        propVals[i].ULongValue = ul;
                        break;
                    case int si:
                        propVals[i].VarType = PropVariant.VT_I4;
                        propVals[i].IntValue = si;
                        break;
                    case bool b:
                        propVals[i].VarType = PropVariant.VT_BOOL;
                        propVals[i].BoolValue = (short)(b ? -1 : 0);
                        break;
                    default:
                        throw new ArgumentException($"Unsupported property value type: {value.GetType().Name}");
                }
                i++;
            }

            setProps.SetCoderProperties((nint)propIds, (nint)propVals, (uint)props.Count);
        }
    }

    private static int GetPropertyHeaderSize(ulong codecId) => codecId switch
    {
        Interop.CodecId.Lzma2 => 1,
        Interop.CodecId.Ppmd => 5,
        Interop.CodecId.Lzma => 5,
        _ => throw new NotSupportedException($"Unknown property header size for codec 0x{codecId:X}")
    };
}
