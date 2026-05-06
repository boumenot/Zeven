using System.IO.Compression;
using System.Runtime.InteropServices;
using Zeven.Core.Interop;

namespace Zeven.Core;

/// <summary>
/// Incremental PPMd compression/decompression stream matching the DeflateStream pattern.
/// PPMd excels at compressing text and structured data.
///
/// Compress mode: Write() pushes uncompressed data through a pipe. A background task
/// runs Code() which pulls from the pipe.
///
/// Decompress mode: Read() returns decompressed data using the decoder's native
/// ISequentialInStream stream-mode — no background thread needed.
/// </summary>
public class PpmdStream : Stream
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

    // Decompress mode state — uses background thread + pipe (PPMd needs known output size)
    private Stream? pipeReader;
    private readonly List<object> liveObjects = new();

    public PpmdStream(Stream stream, CompressionMode mode, bool leaveOpen = false)
        : this(stream, mode, null, leaveOpen) { }

    public PpmdStream(Stream stream, CompressionMode mode, PpmdOptions? options,
        bool leaveOpen = false, int pipeBufferSize = StreamPipe.DefaultBufferSize)
    {
        this.innerStream = stream;
        this.mode = mode;
        this.leaveOpen = leaveOpen;
        this.pipeBufferSize = pipeBufferSize;

        if (mode == CompressionMode.Compress)
        {
            this.InitCompress(options ?? new PpmdOptions());
        }
        else
        {
            this.InitDecompress();
        }
    }

    private void InitCompress(PpmdOptions options)
    {
        var pipe = new StreamPipe(this.pipeBufferSize);
        this.pipeWriter = pipe.WriterStream;
        var pipeReader = pipe.ReaderStream;

        PpmdOptions capturedOptions = options;
        Stream capturedInner = this.innerStream;

        // PPMd needs known input size for the size prefix.
        // Buffer all input via pipe, then compress in batch with size prefix.
        this.backgroundTask = Task.Run(() =>
        {
            try
            {
                using var buffer = new MemoryStream();
                pipeReader.CopyTo(buffer);
                buffer.Position = 0;
                Codec.Compress(capturedOptions, buffer, capturedInner);
            }
            finally
            {
                pipeReader.Dispose();
            }
        });
    }

    private void InitDecompress()
    {
        // PPMd decoder requires known output size — decompress in batch on background thread
        var pipe = new StreamPipe(this.pipeBufferSize);
        var pipeWriter = pipe.WriterStream;
        this.pipeReader = pipe.ReaderStream;

        Stream capturedInner = this.innerStream;

        this.backgroundTask = Task.Run(() =>
        {
            try
            {
                PpmdCodec.Decompress(capturedInner, pipeWriter);
            }
            finally
            {
                pipeWriter.Dispose();
            }
        });
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
        this.CheckBackgroundError();
        if (this.pipeReader == null)
        {
            return 0;
        }
        return this.pipeReader.Read(buffer, offset, count);
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
                this.pipeWriter?.Dispose();
                this.pipeWriter = null;
            }
            else
            {
                this.pipeReader?.Dispose();
                this.pipeReader = null;
            }

            try
            {
                this.backgroundTask?.Wait(TimeSpan.FromSeconds(30));
            }
            catch (AggregateException ae)
            {
                this.backgroundError = ae.InnerException ?? ae;
            }
            this.backgroundTask = null;

            if (!this.leaveOpen)
            {
                try
                {
                    this.innerStream.Dispose();
                }
                catch (COMException)
                {
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
