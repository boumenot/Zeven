using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

#pragma warning disable CS9191 // Marshal.QueryInterface takes ref Guid, not in Guid

namespace Zeven.Core.Interop;

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
/// IArchiveOpenCallback with optional password support.
/// 7-Zip QIs for ICryptoGetTextPassword during Open.
/// </summary>
[GeneratedComClass]
public partial class ArchiveOpenCallback : IArchiveOpenCallback, ICryptoGetTextPassword
{
    private readonly string? _password;
    public ArchiveOpenCallback(string? password = null) => _password = password;

    public int SetTotal(nint files, nint bytes) => 0;
    public int SetCompleted(nint files, nint bytes) => 0;

    public int CryptoGetTextPassword(out nint password)
    {
        if (_password == null)
        {
            password = nint.Zero;
            return unchecked((int)0x80004004); // E_ABORT — no password available
        }
        password = Marshal.StringToBSTR(_password);
        return 0;
    }
}

/// <summary>
/// Managed IOutStream wrapping a .NET Stream (seekable).
/// </summary>
[GeneratedComClass]
public partial class OutStreamWrapper : IOutStream
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

    public int Seek(long offset, uint seekOrigin, nint newPosition)
    {
        ulong pos = (ulong)_stream.Seek(offset, (SeekOrigin)seekOrigin);
        unsafe
        {
            if (newPosition != nint.Zero)
                *(ulong*)newPosition = pos;
        }
        return 0;
    }

    public int SetSize(ulong newSize)
    {
        _stream.SetLength((long)newSize);
        return 0;
    }
}

/// <summary>
/// Extraction callback that writes each item to a MemoryStream.
/// After extraction, results are available in ExtractedStreams.
/// </summary>
[GeneratedComClass]
public partial class ExtractCallback : IArchiveExtractCallback, ICryptoGetTextPassword
{
    private readonly IInArchive _archive;
    private readonly StrategyBasedComWrappers _cw;
    private readonly string? _password;
    private uint _currentIndex;
    private MemoryStream? _currentStream;
    private readonly List<object> _liveObjects = new();

    /// <summary>Extracted data keyed by archive item index.</summary>
    public Dictionary<uint, byte[]> ExtractedData { get; } = new();

    public ExtractCallback(IInArchive archive, StrategyBasedComWrappers cw, string? password = null)
    {
        _archive = archive;
        _cw = cw;
        _password = password;
    }

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
        _liveObjects.Add(wrapper);

        nint ccw = _cw.GetOrCreateComInterfaceForObject(wrapper, CreateComInterfaceFlags.None);
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
            // opRes == 0 means kOK; non-zero means error (CRC, wrong password, etc.)
            if (opRes == 0)
                ExtractedData[_currentIndex] = _currentStream.ToArray();
            _currentStream.Dispose();
            _currentStream = null;
        }
        return 0;
    }

    public int CryptoGetTextPassword(out nint password)
    {
        if (_password == null)
        {
            password = nint.Zero;
            return unchecked((int)0x80004004); // E_ABORT
        }
        password = Marshal.StringToBSTR(_password);
        return 0;
    }
}

/// <summary>
/// Archive creation callback with optional password support.
/// </summary>
[GeneratedComClass]
public partial class UpdateCallback : IArchiveUpdateCallback, ICryptoGetTextPassword2
{
    private readonly List<(string Path, byte[] Data)> _items;
    private readonly StrategyBasedComWrappers _cw;
    private readonly string? _password;
    private readonly List<object> _liveObjects = new();

    public UpdateCallback(Dictionary<string, byte[]> files, StrategyBasedComWrappers cw, string? password = null)
    {
        _items = files.Select(kv => (kv.Key, kv.Value)).ToList();
        _cw = cw;
        _password = password;
    }

    // IProgress
    public int SetTotal(ulong total) => 0;
    public int SetCompleted(nint completeValue) => 0;

    public int GetUpdateItemInfo(uint index, out int newData, out int newProps, out uint indexInArchive)
    {
        newData = 1;
        newProps = 1;
        indexInArchive = unchecked((uint)-1); // no existing item
        return 0;
    }

    public int GetProperty(uint index, uint propID, ref PropVariant value)
    {
        value = default;
        var (path, data) = _items[(int)index];

        switch (propID)
        {
            case PropId.kpidPath:
                value.VarType = PropVariant.VT_BSTR;
                value.PointerValue = Marshal.StringToBSTR(path);
                break;
            case PropId.kpidIsDir:
                value.VarType = PropVariant.VT_BOOL;
                value.BoolValue = 0; // false
                break;
            case PropId.kpidSize:
                value.VarType = PropVariant.VT_UI8;
                value.ULongValue = (ulong)data.Length;
                break;
            case PropId.kpidAttrib:
                value.VarType = PropVariant.VT_UI4;
                value.UIntValue = 0x20; // FILE_ATTRIBUTE_ARCHIVE
                break;
            case PropId.kpidMTime:
                value.VarType = PropVariant.VT_FILETIME;
                value.LongValue = DateTime.UtcNow.ToFileTimeUtc();
                break;
            case 21: // kpidIsAnti
                value.VarType = PropVariant.VT_BOOL;
                value.BoolValue = 0;
                break;
        }
        return 0;
    }

    public int GetStream(uint index, out nint inStream)
    {
        var data = _items[(int)index].Data;
        var ms = new MemoryStream(data, writable: false);
        var wrapper = new InStreamWrapper(ms);
        _liveObjects.Add(ms);
        _liveObjects.Add(wrapper);

        nint ccw = _cw.GetOrCreateComInterfaceForObject(wrapper, CreateComInterfaceFlags.None);
        Guid iid = new("23170F69-40C1-278A-0000-000300010000"); // IID_ISequentialInStream
        Marshal.QueryInterface(ccw, ref iid, out inStream);
        Marshal.Release(ccw);
        return 0;
    }

    public int SetOperationResult(int operationResult) => 0;

    public int CryptoGetTextPassword2(out int passwordIsDefined, out nint password)
    {
        if (_password != null)
        {
            passwordIsDefined = 1;
            password = Marshal.StringToBSTR(_password);
        }
        else
        {
            passwordIsDefined = 0;
            password = nint.Zero;
        }
        return 0;
    }
}
