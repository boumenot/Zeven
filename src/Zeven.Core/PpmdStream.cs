using System.IO.Compression;
using Zeven.Core.Interop;

namespace Zeven.Core;

/// <summary>
/// Incremental PPMd compression/decompression stream matching the DeflateStream pattern.
/// Uses the Zeven chunked wire format — no background threads.
///
/// Compress mode: buffers writes into a chunk buffer, flushing full chunks to the
/// inner stream via <see cref="ZevenFormat"/> framing.
///
/// Decompress mode: reads chunks from the inner stream, decompresses each one, and
/// serves data from the decompressed buffer.
/// </summary>
public class PpmdStream : Stream
{
    private readonly Stream innerStream;
    private readonly CompressionMode mode;
    private readonly bool leaveOpen;
    private bool disposed;

    // Compress mode state
    private readonly PpmdOptions? options;
    private readonly byte[]? propertyHeader;
    private byte[]? chunkBuffer;
    private int chunkBufferUsed;
    private bool headerWritten;

    // Decompress mode state
    private readonly byte[]? decompressPropertyHeader;
    private MemoryStream? decompressedBuffer;
    private bool eof;

    public PpmdStream(Stream stream, CompressionMode mode, bool leaveOpen = false)
        : this(stream, mode, null, leaveOpen) { }

    public PpmdStream(Stream stream, CompressionMode mode, PpmdOptions? options,
            bool leaveOpen = false)
    {
        this.innerStream = stream;
        this.mode = mode;
        this.leaveOpen = leaveOpen;

        if (mode == CompressionMode.Compress)
        {
            this.options = options ?? new PpmdOptions();
            this.propertyHeader = Codec.CapturePropertyHeader(this.options);
            this.chunkBuffer = new byte[this.options.ChunkSize];
            this.chunkBufferUsed = 0;
            this.headerWritten = false;
        }
        else
        {
            this.decompressPropertyHeader = ZevenFormat.ReadHeader(stream).PropertyHeader;
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
            int space = this.chunkBuffer!.Length - this.chunkBufferUsed;
            int toCopy = Math.Min(space, count);
            Buffer.BlockCopy(buffer, offset, this.chunkBuffer, this.chunkBufferUsed, toCopy);
            this.chunkBufferUsed += toCopy;
            offset += toCopy;
            count -= toCopy;

            if (this.chunkBufferUsed == this.chunkBuffer.Length)
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
            if (this.mode == CompressionMode.Compress)
            {
                this.FlushChunk();

                if (!this.headerWritten)
                {
                    ZevenFormat.WriteHeader(this.innerStream, CodecId.Ppmd, this.propertyHeader!);
                    this.headerWritten = true;
                }

                ZevenFormat.WriteEndMarker(this.innerStream);
            }

            this.decompressedBuffer?.Dispose();
            this.decompressedBuffer = null;

            if (!this.leaveOpen)
            {
                this.innerStream.Dispose();
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
            ZevenFormat.WriteHeader(this.innerStream, CodecId.Ppmd, this.propertyHeader!);
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
        Codec.DecompressBlock(this.decompressPropertyHeader!, CodecId.Ppmd,
                compressedStream, decompressed, chunk.Value.UncompressedSize);

        decompressed.Position = 0;
        this.decompressedBuffer = decompressed;
    }
}
