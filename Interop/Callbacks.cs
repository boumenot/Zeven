using System.Runtime.InteropServices;
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

/// <summary>
/// Managed ISequentialOutStream wrapping a .NET Stream.
/// </summary>
[GeneratedComClass]
public partial class OutStreamWrapper : ISequentialOutStream
{
    private readonly Stream _stream;
    public OutStreamWrapper(Stream stream) => _stream = stream;

    public int Write(nint data, uint size, out uint processedSize)
    {
        if (size == 0) { processedSize = 0; return 0; }
        unsafe
        {
            var span = new ReadOnlySpan<byte>((void*)data, (int)size);
            _stream.Write(span);
            processedSize = size;
        }
        return 0;
    }
}

/// <summary>
/// Extraction callback that writes each item to a MemoryStream.
/// After extraction, results are available in ExtractedStreams.
/// </summary>
[GeneratedComClass]
public partial class ExtractCallback : IArchiveExtractCallback
{
    private readonly IInArchive _archive;
    private uint _currentIndex;
    private MemoryStream? _currentStream;

    /// <summary>Extracted data keyed by archive item index.</summary>
    public Dictionary<uint, byte[]> ExtractedData { get; } = new();

    public ExtractCallback(IInArchive archive) => _archive = archive;

    // IProgress
    public int SetTotal(ulong total) => 0;
    public int SetCompleted(nint completeValue) => 0;

    // IArchiveExtractCallback
    public int GetStream(uint index, out nint outStream, int askExtractMode)
    {
        _currentIndex = index;
        _currentStream = null;
        outStream = nint.Zero;

        // Only create output stream for actual extraction (not test/skip)
        if (askExtractMode != 0) // 0 = kExtract
            return 0;

        // Skip directories
        PropVariant pv = default;
        _archive.GetProperty(index, PropId.kpidIsDir, ref pv);
        bool isDir = pv.GetBool();
        if (isDir) return 0;

        _currentStream = new MemoryStream();
        var wrapper = new OutStreamWrapper(_currentStream);

        // Create CCW — we need a StrategyBasedComWrappers for this
        // Use a static instance since we don't have the handle's instance here
        var cw = new StrategyBasedComWrappers();
        nint ccw = cw.GetOrCreateComInterfaceForObject(wrapper, CreateComInterfaceFlags.None);
        Guid iid = new("23170F69-40C1-278A-0000-000300020000"); // IID_ISequentialOutStream
        Marshal.QueryInterface(ccw, ref iid, out outStream);
        Marshal.Release(ccw);
        return 0;
    }

    public int PrepareOperation(int askExtractMode) => 0;

    public int SetOperationResult(int opRes)
    {
        if (_currentStream != null)
        {
            ExtractedData[_currentIndex] = _currentStream.ToArray();
            _currentStream.Dispose();
            _currentStream = null;
        }
        return 0;
    }
}
