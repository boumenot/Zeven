using System.Buffers;
using System.IO.Compression;
using Zeven.Core.Interop;

namespace Zeven.Core;

/// <summary>
/// Generic base class for incremental compression/decompression streams using
/// the Zeven chunked wire format. Typed wrappers (PpmdStream, ZstdStream, etc.)
/// inherit from this class and forward constructors.
///
/// Compress mode: buffers writes into a chunk buffer, flushing full chunks to the
/// inner stream via <see cref="ZevenFormat"/> framing.
///
/// Decompress mode: reads chunks from the inner stream, decompresses each one, and
/// serves data from the decompressed buffer.
///
/// The chunk buffer is rented from <see cref="ArrayPool{T}"/> to avoid LOH allocations.
/// </summary>
public class ZevenStream<TOptions> : Stream where TOptions : ICodecOptions, new()
{
    private readonly Stream innerStream;
    private readonly CompressionMode mode;
    private readonly bool leaveOpen;
    private readonly int chunkSize;
    private bool disposed;

    // Compress mode state
    private readonly TOptions? options;
    private readonly byte[]? propertyHeader;
    private byte[]? chunkBuffer;
    private int chunkBufferUsed;
    private bool headerWritten;

    // Decompress mode state
    private readonly ulong decompressCodecId;
    private readonly byte[]? decompressPropertyHeader;
    private MemoryStream? decompressedBuffer;
    private bool eof;

    public ZevenStream(Stream stream, CompressionMode mode, bool leaveOpen = false)
        : this(stream, mode, default, leaveOpen) { }

    public ZevenStream(Stream stream, CompressionMode mode, TOptions? options,
            bool leaveOpen = false)
    {
        this.innerStream = stream;
        this.mode = mode;
        this.leaveOpen = leaveOpen;

        if (mode == CompressionMode.Compress)
        {
            this.options = options ?? new TOptions();
            this.propertyHeader = Codec.CapturePropertyHeader(this.options);
            this.chunkSize = this.options.ChunkSize;
            if (this.chunkSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "ChunkSize must be positive.");
            }
            this.chunkBuffer = ArrayPool<byte>.Shared.Rent(this.chunkSize);
            this.chunkBufferUsed = 0;
            this.headerWritten = false;
        }
        else
        {
            this.decompressCodecId = new TOptions().CodecId;
            this.decompressPropertyHeader = ZevenFormat.ReadHeaderAndValidateCodec(
                    stream, this.decompressCodecId);
        }
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

        int totalRead = 0;
        while (totalRead < count)
        {
            if (this.decompressedBuffer != null
                && this.decompressedBuffer.Position < this.decompressedBuffer.Length)
            {
                int available = (int)(this.decompressedBuffer.Length
                    - this.decompressedBuffer.Position);
                int toRead = Math.Min(available, count - totalRead);
                this.decompressedBuffer.Read(buffer, offset + totalRead, toRead);
                totalRead += toRead;
                continue;
            }

            if (this.eof)
            {
                break;
            }

            this.LoadNextChunk();
        }

        return totalRead;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (this.mode != CompressionMode.Compress)
        {
            throw new InvalidOperationException("Cannot write in decompress mode");
        }

        while (count > 0)
        {
            int space = this.chunkSize - this.chunkBufferUsed;
            int toCopy = Math.Min(space, count);
            Buffer.BlockCopy(buffer, offset, this.chunkBuffer, this.chunkBufferUsed, toCopy);
            this.chunkBufferUsed += toCopy;
            offset += toCopy;
            count -= toCopy;

            if (this.chunkBufferUsed == this.chunkSize)
            {
                this.FlushChunk();
            }
        }
    }

    public override void Flush()
    {
        if (this.mode == CompressionMode.Compress)
        {
            this.FlushChunk();
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
            try
            {
                if (this.mode == CompressionMode.Compress)
                {
                    this.FlushChunk();

                    if (!this.headerWritten)
                    {
                        ZevenFormat.WriteHeader(this.innerStream,
                                this.options!.CodecId, this.propertyHeader!);
                        this.headerWritten = true;
                    }

                    ZevenFormat.WriteEndMarker(this.innerStream);
                }
            }
            finally
            {
                if (this.chunkBuffer != null)
                {
                    ArrayPool<byte>.Shared.Return(this.chunkBuffer);
                    this.chunkBuffer = null;
                }

                this.decompressedBuffer?.Dispose();
                this.decompressedBuffer = null;

                if (!this.leaveOpen)
                {
                    this.innerStream.Dispose();
                }
            }
        }

        base.Dispose(disposing);
    }

    private void FlushChunk()
    {
        if (this.chunkBufferUsed == 0)
        {
            return;
        }

        if (!this.headerWritten)
        {
            ZevenFormat.WriteHeader(this.innerStream,
                    this.options!.CodecId, this.propertyHeader!);
            this.headerWritten = true;
        }

        using var inputStream = new MemoryStream(
            this.chunkBuffer!, 0, this.chunkBufferUsed, writable: false);
        using var compressed = new MemoryStream();
        Codec.CompressBlock(this.options!, this.propertyHeader!, inputStream, compressed);

        ZevenFormat.WriteChunk(this.innerStream, this.chunkBufferUsed,
                compressed.GetBuffer().AsSpan(0, (int)compressed.Length));

        this.chunkBufferUsed = 0;
    }

    private void LoadNextChunk()
    {
        var chunk = ZevenFormat.ReadChunk(this.innerStream);
        if (chunk == null)
        {
            this.eof = true;
            return;
        }

        this.decompressedBuffer?.Dispose();

        using var compressedStream = new MemoryStream(chunk.Value.CompressedData);
        var decompressed = new MemoryStream();
        Codec.DecompressBlock(this.decompressPropertyHeader!, this.decompressCodecId,
                compressedStream, decompressed, chunk.Value.UncompressedSize);

        decompressed.Position = 0;
        this.decompressedBuffer = decompressed;
    }
}
