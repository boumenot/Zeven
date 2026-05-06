using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Zeven.Core.Interop;

#pragma warning disable CS9191 // Marshal.QueryInterface takes ref Guid, not in Guid

namespace Zeven.Core;

/// <summary>
/// A fixed-size ring buffer that connects a writer Stream to a reader Stream.
/// Writer blocks when the buffer is full; reader blocks when it's empty.
/// When the writer is disposed, the reader gets EOF (Read returns 0).
/// No per-Write allocations — data is copied directly into/out of the ring buffer.
/// </summary>
public sealed class StreamPipe
{
    /// <summary>Default buffer size: 64KB — optimal for throughput and GC pressure.</summary>
    public const int DefaultBufferSize = 1 << 16;

    private readonly byte[] buffer;
    private int readPos;
    private int writePos;
    private int count;
    private bool writerDone;
    private readonly Lock syncLock = new();
    private readonly ManualResetEventSlim dataAvailable = new(false);
    private readonly ManualResetEventSlim spaceAvailable = new(true);

    public StreamPipe(int bufferSize = DefaultBufferSize)
    {
        if (bufferSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(bufferSize));
        }
        this.buffer = new byte[bufferSize];
    }

    public Stream WriterStream => new WriterSide(this);
    public Stream ReaderStream => new ReaderSide(this);

    private void WriteData(byte[] data, int offset, int length)
    {
        while (length > 0)
        {
            int written = 0;
            lock (this.syncLock)
            {
                int free = this.buffer.Length - this.count;
                if (free > 0)
                {
                    int toCopy = Math.Min(length, free);
                    // Copy in up to two segments (wrap-around)
                    int firstLen = Math.Min(toCopy, this.buffer.Length - this.writePos);
                    Buffer.BlockCopy(data, offset, this.buffer, this.writePos, firstLen);
                    if (toCopy > firstLen)
                    {
                        Buffer.BlockCopy(data, offset + firstLen, this.buffer, 0, toCopy - firstLen);
                    }
                    this.writePos = (this.writePos + toCopy) % this.buffer.Length;
                    this.count += toCopy;
                    written = toCopy;
                    this.dataAvailable.Set();
                    if (this.count == this.buffer.Length)
                    {
                        this.spaceAvailable.Reset();
                    }
                }
                else
                {
                    this.spaceAvailable.Reset();
                }
            }

            if (written > 0)
            {
                offset += written;
                length -= written;
            }
            else
            {
                this.spaceAvailable.Wait();
            }
        }
    }

    private int ReadData(byte[] data, int offset, int length)
    {
        while (true)
        {
            lock (this.syncLock)
            {
                if (this.count > 0)
                {
                    int toCopy = Math.Min(length, this.count);
                    // Copy out up to two segments (wrap-around)
                    int firstLen = Math.Min(toCopy, this.buffer.Length - this.readPos);
                    Buffer.BlockCopy(this.buffer, this.readPos, data, offset, firstLen);
                    if (toCopy > firstLen)
                    {
                        Buffer.BlockCopy(this.buffer, 0, data, offset + firstLen, toCopy - firstLen);
                    }
                    this.readPos = (this.readPos + toCopy) % this.buffer.Length;
                    this.count -= toCopy;
                    this.spaceAvailable.Set();
                    if (this.count == 0)
                    {
                        this.dataAvailable.Reset();
                    }
                    return toCopy;
                }

                if (this.writerDone)
                {
                    return 0; // EOF
                }
                this.dataAvailable.Reset();
            }

            this.dataAvailable.Wait();
        }
    }

    private void CompleteWriter()
    {
        lock (this.syncLock)
        {
            this.writerDone = true;
            this.dataAvailable.Set(); // wake reader if waiting
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
                pipe.WriteData(buffer, offset, count);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                pipe.CompleteWriter();
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
    private readonly int pipeBufferSize;
    private bool disposed;

    // Compress mode state
    private Stream? pipeWriter;
    private Task? backgroundTask;
    private Exception? backgroundError;

    // Decompress mode state — native stream-mode decoder
    private nint decoderInStreamPtr;
    private readonly List<object> liveObjects = new();

    public Lzma2Stream(Stream stream, CompressionMode mode, bool leaveOpen = false)
        : this(stream, mode, null, leaveOpen) { }

    public Lzma2Stream(Stream stream, CompressionMode mode, Lzma2Options? options,
        bool leaveOpen = false, int pipeBufferSize = StreamPipe.DefaultBufferSize)
    {
        this.innerStream = stream;
        this.mode = mode;
        this.leaveOpen = leaveOpen;
        this.pipeBufferSize = pipeBufferSize;

        if (mode == CompressionMode.Compress)
        {
            this.InitCompress(options ?? new Lzma2Options());
        }
        else
        {
            this.InitDecompress();
        }
    }

    private void InitCompress(Lzma2Options options)
    {
        var pipe = new StreamPipe(this.pipeBufferSize);
        this.pipeWriter = pipe.WriterStream;
        var pipeReader = pipe.ReaderStream;

        Lzma2Options capturedOptions = options;
        Stream capturedInner = this.innerStream;

        this.backgroundTask = Task.Run(() =>
        {
            try
            {
                Codec.Compress(capturedOptions, pipeReader, capturedInner, writeSizePrefix: false);
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
        this.decoderInStreamPtr = Codec.InitStreamDecoder(
            CodecId.Lzma2, Lzma2Codec.Lzma2PropertyHeaderSize,
            this.innerStream, lib.ComWrappers, this.liveObjects, hasSizePrefix: false);
    }

    public override bool CanRead=> this.mode == CompressionMode.Decompress && !this.disposed;
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
