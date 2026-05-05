using System.Runtime.InteropServices.Marshalling;

namespace SevenZipNet.Interop;

/// <summary>
/// Managed IInStream implementation wrapping a .NET Stream.
/// Passed to native code as a COM Callable Wrapper via [GeneratedComClass].
/// </summary>
[GeneratedComClass]
public partial class InStreamWrapper : IInStream
{
    private readonly Stream _stream;

    public InStreamWrapper(Stream stream) => _stream = stream;

    public int Read(nint data, uint size, out uint processedSize)
    {
        if (size == 0) { processedSize = 0; return 0; }
        try
        {
            unsafe
            {
                var span = new Span<byte>((void*)data, (int)size);
                int bytesRead = _stream.Read(span);
                processedSize = (uint)bytesRead;
            }
            return 0; // S_OK
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [Read ERROR] {ex.Message}");
            processedSize = 0;
            return unchecked((int)0x80004005);
        }
    }

    public int Seek(long offset, uint seekOrigin, nint newPosition)
    {
        try
        {
            ulong pos = (ulong)_stream.Seek(offset, (SeekOrigin)seekOrigin);
            unsafe
            {
                if (newPosition != nint.Zero)
                    *(ulong*)newPosition = pos;
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($" [Seek ERROR] {ex.Message}");
            Console.Out.Flush();
            return unchecked((int)0x80004005);
        }
    }
}

/// <summary>
/// Minimal IArchiveOpenCallback — ignores progress, no password support.
/// </summary>
[GeneratedComClass]
public partial class ArchiveOpenCallback : IArchiveOpenCallback
{
    public int SetTotal(nint files, nint bytes) => 0;
    public int SetCompleted(nint files, nint bytes) => 0;
}
