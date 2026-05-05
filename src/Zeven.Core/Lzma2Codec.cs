using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Zeven.Core.Interop;

#pragma warning disable CS9191 // Marshal.QueryInterface takes ref Guid, not in Guid

namespace Zeven.Core;

/// <summary>
/// Batch LZMA2 compression/decompression using 7-Zip's ICompressCoder.
/// Processes entire streams in one call — for incremental streaming, use Lzma2Stream.
/// </summary>
public static class Lzma2Codec
{
    private const ulong Lzma2CodecId = 0x21;

    /// <summary>Compress a stream using LZMA2. Writes a 1-byte property header then compressed data.</summary>
    public static void Compress(Stream input, Stream output, int level = 5)
    {
        var lib = ZevenLibrary.Load("");
        int codecIndex = lib.FindCodecIndex(Lzma2CodecId);
        if (codecIndex < 0)
        {
            throw new InvalidOperationException("LZMA2 codec not found in 7z.dll");
        }

        nint encoderPtr = lib.CreateEncoderObject((uint)codecIndex);
        var cw = lib.ComWrappers;

        // Set compression level
        Guid iidSetProps = Iid.ICompressSetCoderProperties;
        Marshal.QueryInterface(encoderPtr, ref iidSetProps, out nint setPropsPtr);
        if (setPropsPtr != nint.Zero)
        {
            var setProps = (ICompressSetCoderProperties)cw.GetOrCreateObjectForComInstance(
                setPropsPtr, CreateObjectFlags.UniqueInstance);
            SetLevel(setProps, level);
            Marshal.Release(setPropsPtr);
        }

        // Write 1-byte property header
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

    /// <summary>Decompress an LZMA2 stream. Reads the 1-byte property header then decompresses.</summary>
    public static void Decompress(Stream input, Stream output)
    {
        // Read 1-byte property header
        int propByte = input.ReadByte();
        if (propByte < 0)
        {
            throw new InvalidDataException("Unexpected end of stream reading LZMA2 property byte");
        }

        var lib = ZevenLibrary.Load("");
        int codecIndex = lib.FindCodecIndex(Lzma2CodecId);
        if (codecIndex < 0)
        {
            throw new InvalidOperationException("LZMA2 codec not found in 7z.dll");
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
                byte prop = (byte)propByte;
                setDecProps.SetDecoderProperties2((nint)(&prop), 1);
            }
            Marshal.Release(setDecPropsPtr);
        }

        // Decode
        var coder = (ICompressCoder)cw.GetOrCreateObjectForComInstance(
            decoderPtr, CreateObjectFlags.UniqueInstance);
        CodeStreams(coder, cw, input, output);
    }

    private static void CodeStreams(ICompressCoder coder, StrategyBasedComWrappers cw, Stream input, Stream output)
    {
        var inWrapper = new InStreamWrapper(input);
        var outWrapper = new OutStreamWrapper(output);

        nint inCcw = cw.GetOrCreateComInterfaceForObject(inWrapper, CreateComInterfaceFlags.None);
        Guid iidSeqIn = Iid.ISequentialInStream;
        Marshal.QueryInterface(inCcw, ref iidSeqIn, out nint inPtr);

        nint outCcw = cw.GetOrCreateComInterfaceForObject(outWrapper, CreateComInterfaceFlags.None);
        Guid iidSeqOut = Iid.ISequentialOutStream;
        Marshal.QueryInterface(outCcw, ref iidSeqOut, out nint outPtr);

        int hr = coder.Code(inPtr, outPtr, nint.Zero, nint.Zero, nint.Zero);

        if (inPtr != nint.Zero) { Marshal.Release(inPtr); }
        if (outPtr != nint.Zero) { Marshal.Release(outPtr); }
        Marshal.Release(inCcw);
        Marshal.Release(outCcw);
        GC.KeepAlive(inWrapper);
        GC.KeepAlive(outWrapper);

        Marshal.ThrowExceptionForHR(hr);
    }

    private static void SetLevel(ICompressSetCoderProperties setProps, int level)
    {
        unsafe
        {
            uint propId = CoderPropId.Level;
            PropVariant propVal = default;
            propVal.VarType = PropVariant.VT_UI4;
            propVal.UIntValue = (uint)level;

            setProps.SetCoderProperties((nint)(&propId), (nint)(&propVal), 1);
        }
    }
}
