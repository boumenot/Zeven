using System.Collections.Concurrent;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Zeven.Core.Interop;

#pragma warning disable CS9191 // Marshal.QueryInterface takes ref Guid, not in Guid

namespace Zeven.Core;

/// <summary>
/// A simple in-process pipe using a BlockingCollection of byte[] chunks.
/// Writer writes chunks; reader reads from them sequentially.
/// When writer is completed, reader gets EOF (Read returns 0).
/// </summary>
internal sealed class StreamPipe
{
    private readonly BlockingCollection<byte[]> queue = new(boundedCapacity: 64);
    private byte[]? current;
    private int currentOffset;

    public Stream WriterStream => new WriterSide(this);
    public Stream ReaderStream => new ReaderSide(this);

    private void WriteChunk(byte[] data) => this.queue.Add(data);
    private void Complete() => this.queue.CompleteAdding();

    private int ReadData(byte[] buffer, int offset, int count)
    {
        while (true)
        {
            if (this.current != null && this.currentOffset < this.current.Length)
            {
                int available = this.current.Length - this.currentOffset;
                int toCopy = Math.Min(available, count);
                Buffer.BlockCopy(this.current, this.currentOffset, buffer, offset, toCopy);
                this.currentOffset += toCopy;
                return toCopy;
            }

            if (!this.queue.TryTake(out this.current, Timeout.Infinite))
            {
                return 0; // completed
            }
            this.currentOffset = 0;
        }
    }

    private sealed class WriterSide(StreamPipe pipe) : Stream
    {
        public override bool CanRead => false;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Flush() { }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (count > 0)
            {
                var chunk = new byte[count];
                Buffer.BlockCopy(buffer, offset, chunk, 0, count);
                pipe.WriteChunk(chunk);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                pipe.Complete();
            }
            base.Dispose(disposing);
        }
    }

    private sealed class ReaderSide(StreamPipe pipe) : Stream
    {
        public override bool CanRead => true;
        public override bool CanWrite => false;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => pipe.ReadData(buffer, offset, count);
    }
}

/// <summary>
/// Incremental LZMA2 compression/decompression stream matching the DeflateStream pattern.
///
/// Compress mode: Write() pushes uncompressed data through a pipe. A background task
/// runs Code() which pulls from the pipe. Dispose() signals EOF and waits for completion.
///
/// Decompress mode: Read() returns decompressed data using the decoder's native
/// ISequentialInStream stream-mode — no background thread needed.
/// </summary>
public class Lzma2Stream : Stream
{
    private readonly Stream innerStream;
    private readonly CompressionMode mode;
    private readonly bool leaveOpen;
    private bool disposed;

    // Compress mode state
    private Stream? pipeWriter;
    private Task? backgroundTask;
    private Exception? backgroundError;

    // Decompress mode state — native stream-mode decoder
    private nint decoderInStreamPtr;
    private readonly List<object> liveObjects = new();

    public Lzma2Stream(Stream stream, CompressionMode mode, bool leaveOpen = false)
        : this(stream, mode, 5, leaveOpen) { }

    public Lzma2Stream(Stream stream, CompressionMode mode, int level, bool leaveOpen = false)
    {
        this.innerStream = stream;
        this.mode = mode;
        this.leaveOpen = leaveOpen;

        if (mode == CompressionMode.Compress)
        {
            this.InitCompress(level);
        }
        else
        {
            this.InitDecompress();
        }
    }

    private void InitCompress(int level)
    {
        var pipe = new StreamPipe();
        this.pipeWriter = pipe.WriterStream;
        var pipeReader = pipe.ReaderStream;

        int capturedLevel = level;
        Stream capturedInner = this.innerStream;

        this.backgroundTask = Task.Run(() =>
        {
            try
            {
                Lzma2Codec.Compress(pipeReader, capturedInner, capturedLevel);
            }
            finally
            {
                pipeReader.Dispose();
            }
        });
    }

    private void InitDecompress()
    {
        var lib = ZevenLibrary.Instance;
        int codecIndex = lib.FindCodecIndex(CodecId.Lzma2);
        if (codecIndex < 0)
        {
            throw new InvalidOperationException("LZMA2 codec not found");
        }

        // Read 1-byte property header
        int propByte = this.innerStream.ReadByte();
        if (propByte < 0)
        {
            throw new InvalidDataException("Unexpected end of stream reading LZMA2 property byte");
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

        // Set input stream (the compressed inner stream)
        var inWrapper = new InStreamWrapper(this.innerStream);
        this.liveObjects.Add(inWrapper);
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

        // Initialize for stream-mode decoding (unknown output size)
        Guid iidSetOutSize = Iid.ICompressSetOutStreamSize;
        Marshal.QueryInterface(decoderPtr, ref iidSetOutSize, out nint setOutSizePtr);
        if (setOutSizePtr != nint.Zero)
        {
            var setOutSize = (ICompressSetOutStreamSize)cw.GetOrCreateObjectForComInstance(
                setOutSizePtr, CreateObjectFlags.UniqueInstance);
            setOutSize.SetOutStreamSize(nint.Zero); // NULL = unknown size
            Marshal.Release(setOutSizePtr);
        }

        // QI decoder for ISequentialInStream — this IS the decompressed data stream
        Marshal.QueryInterface(decoderPtr, ref iidSeqIn, out this.decoderInStreamPtr);
    }

    public override bool CanRead => this.mode == CompressionMode.Decompress && !this.disposed;
    public override bool CanWrite => this.mode == CompressionMode.Compress && !this.disposed;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (this.mode != CompressionMode.Decompress)
        {
            throw new InvalidOperationException("Cannot read in compress mode");
        }
        if (this.decoderInStreamPtr == nint.Zero)
        {
            return 0;
        }

        // Call the decoder's native ISequentialInStream.Read() directly via vtable
        unsafe
        {
            fixed (byte* pBuf = &buffer[offset])
            {
                nint vtable = *(nint*)this.decoderInStreamPtr;
                // ISequentialInStream vtable: [0]=QI, [1]=AddRef, [2]=Release, [3]=Read
                var readFunc = (delegate* unmanaged[Stdcall]<nint, byte*, uint, uint*, int>)(
                    *((nint*)vtable + 3));
                uint processedSize;
                int hr = readFunc(this.decoderInStreamPtr, pBuf, (uint)count, &processedSize);
                if (hr < 0)
                {
                    Marshal.ThrowExceptionForHR(hr);
                }
                return (int)processedSize;
            }
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (this.mode != CompressionMode.Compress)
        {
            throw new InvalidOperationException("Cannot write in decompress mode");
        }
        this.CheckBackgroundError();
        this.pipeWriter!.Write(buffer, offset, count);
    }

    public override void Flush()
    {
        if (this.mode == CompressionMode.Compress && this.pipeWriter != null)
        {
            this.pipeWriter.Flush();
        }
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (this.disposed)
        {
            base.Dispose(disposing);
            return;
        }
        this.disposed = true;

        if (disposing)
        {
            if (this.mode == CompressionMode.Compress)
            {
                // Close the pipe writer — signals EOF to the background task's input
                this.pipeWriter?.Dispose();
                this.pipeWriter = null;

                // Wait for background task to finish
                try
                {
                    this.backgroundTask?.Wait(TimeSpan.FromSeconds(30));
                }
                catch (AggregateException ae)
                {
                    this.backgroundError = ae.InnerException ?? ae;
                }

                this.backgroundTask = null;
            }
            else
            {
                // Decompress: release the native decoder's ISequentialInStream
                if (this.decoderInStreamPtr != nint.Zero)
                {
                    Marshal.Release(this.decoderInStreamPtr);
                    this.decoderInStreamPtr = nint.Zero;
                }
            }

            if (!this.leaveOpen)
            {
                try
                {
                    this.innerStream.Dispose();
                }
                catch (COMException)
                {
                    // Inner stream may already be released by COM cleanup
                }
            }

            this.CheckBackgroundError();
        }

        base.Dispose(disposing);
    }

    private void CheckBackgroundError()
    {
        if (this.backgroundError != null)
        {
            var ex = this.backgroundError;
            this.backgroundError = null;
            throw ex;
        }

        if (this.backgroundTask is { IsFaulted: true } task)
        {
            var ex = task.Exception?.InnerException ?? task.Exception!;
            this.backgroundTask = null;
            throw ex;
        }
    }
}
