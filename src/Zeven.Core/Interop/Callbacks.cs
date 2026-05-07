using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Zeven.Core;

#pragma warning disable CS9191 // Marshal.QueryInterface takes ref Guid, not in Guid

namespace Zeven.Core.Interop;

/// <summary>
/// Managed IInStream implementation wrapping a .NET Stream.
/// Passed to native code as a COM Callable Wrapper via [GeneratedComClass].
/// </summary>
[GeneratedComClass]
public partial class InStreamWrapper : IInStream
{
    private readonly Stream stream;

    public InStreamWrapper(Stream stream) => this.stream = stream;

    public int Read(nint data, uint size, out uint processedSize)
    {
        if (size == 0)
        {
            processedSize = 0;
            return 0;
        }
        unsafe
        {
            var span = new Span<byte>((void*)data, (int)size);
            int bytesRead = this.stream.Read(span);
            processedSize = (uint)bytesRead;
        }
        return 0;
    }

    public int Seek(long offset, uint seekOrigin, nint newPosition)
    {
        ulong pos = (ulong)this.stream.Seek(offset, (SeekOrigin)seekOrigin);
        unsafe
        {
            if (newPosition != nint.Zero)
            {
                *(ulong*)newPosition = pos;
            }
        }
        return 0;
    }
}

/// <summary>
/// IArchiveOpenCallback with optional password support.
/// 7-Zip QIs for ICryptoGetTextPassword during Open.
/// </summary>
[GeneratedComClass]
public partial class ArchiveOpenCallback : IArchiveOpenCallback, ICryptoGetTextPassword
{
    private readonly string? password;
    public ArchiveOpenCallback(string? password = null) => this.password = password;

    public int SetTotal(nint files, nint bytes) => 0;
    public int SetCompleted(nint files, nint bytes) => 0;

    public int CryptoGetTextPassword(out nint password)
    {
        if (this.password == null)
        {
            password = nint.Zero;
            return unchecked((int)0x80004004); // E_ABORT — no password available
        }
        password = Marshal.StringToBSTR(this.password);
        return 0;
    }
}

/// <summary>
/// Managed IOutStream wrapping a .NET Stream (seekable).
/// </summary>
[GeneratedComClass]
public partial class OutStreamWrapper : IOutStream
{
    private readonly Stream stream;
    public OutStreamWrapper(Stream stream) => this.stream = stream;

    public int Write(nint data, uint size, out uint processedSize)
    {
        if (size == 0)
        {
            processedSize = 0;
            return 0;
        }
        unsafe
        {
            var span = new ReadOnlySpan<byte>((void*)data, (int)size);
            this.stream.Write(span);
            processedSize = size;
        }
        return 0;
    }

    public int Seek(long offset, uint seekOrigin, nint newPosition)
    {
        ulong pos = (ulong)this.stream.Seek(offset, (SeekOrigin)seekOrigin);
        unsafe
        {
            if (newPosition != nint.Zero)
            {
                *(ulong*)newPosition = pos;
            }
        }
        return 0;
    }

    public int SetSize(ulong newSize)
    {
        this.stream.SetLength((long)newSize);
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
    private readonly IInArchive archive;
    private readonly StrategyBasedComWrappers comWrappers;
    private readonly string? password;
    private readonly IProgress<ArchiveProgress>? progress;
    private readonly CancellationToken cancellationToken;
    private ulong totalBytes;
    private uint currentIndex;
    private MemoryStream? currentStream;
    private readonly List<object> liveObjects = new();

    /// <summary>Extracted data keyed by archive item index.</summary>
    public Dictionary<uint, byte[]> ExtractedData { get; } = new();

    /// <summary>Failures collected during extraction.</summary>
    public List<ExtractionFailure> Failures { get; } = new();

    public ExtractCallback(IInArchive archive, StrategyBasedComWrappers cw,
            string? password = null,
            IProgress<ArchiveProgress>? progress = null,
            CancellationToken cancellationToken = default)
    {
        this.archive = archive;
        this.comWrappers = cw;
        this.password = password;
        this.progress = progress;
        this.cancellationToken = cancellationToken;
    }

    // IProgress
    public int SetTotal(ulong total)
    {
        this.totalBytes = total;
        return 0;
    }

    public int SetCompleted(nint completeValue)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return unchecked((int)0x80004004); // E_ABORT
        }

        if (this.progress != null && completeValue != nint.Zero)
        {
            unsafe
            {
                ulong completed = *(ulong*)completeValue;
                this.progress.Report(new ArchiveProgress(this.totalBytes, completed));
            }
        }
        return 0;
    }

    // IArchiveExtractCallback
    public int GetStream(uint index, out nint outStream, int askExtractMode)
    {
        this.currentIndex = index;
        this.currentStream = null;
        outStream = nint.Zero;

        // Only create output stream for actual extraction (not test/skip)
        if (askExtractMode != 0) // 0 = kExtract
        {
            return 0;
        }

        // Skip directories
        PropVariant pv = default;
        this.archive.GetProperty(index, PropId.kpidIsDir, ref pv);
        bool isDir = pv.GetBool();
        if (isDir)
        {
            return 0;
        }

        this.currentStream = new MemoryStream();
        var wrapper = new OutStreamWrapper(this.currentStream);
        this.liveObjects.Add(wrapper);

        nint ccw = this.comWrappers.GetOrCreateComInterfaceForObject(wrapper, CreateComInterfaceFlags.None);
        Guid iid = Iid.ISequentialOutStream;
        Marshal.QueryInterface(ccw, ref iid, out outStream);
        Marshal.Release(ccw);
        return 0;
    }

    public int PrepareOperation(int askExtractMode) => 0;

    public int SetOperationResult(int opRes)
    {
        if (this.currentStream != null)
        {
            if (opRes == 0)
            {
                this.ExtractedData[this.currentIndex] = this.currentStream.ToArray();
            }
            this.currentStream.Dispose();
            this.currentStream = null;
        }

        if (opRes != 0)
        {
            this.Failures.Add(new ExtractionFailure(this.currentIndex, (ExtractionResult)opRes));
        }

        return 0;
    }

    public int CryptoGetTextPassword(out nint password)
    {
        if (this.password == null)
        {
            password = nint.Zero;
            return unchecked((int)0x80004004); // E_ABORT
        }
        password = Marshal.StringToBSTR(this.password);
        return 0;
    }
}

/// <summary>
/// Archive creation callback with optional password support.
/// </summary>
[GeneratedComClass]
public partial class UpdateCallback : IArchiveUpdateCallback, ICryptoGetTextPassword2
{
    private readonly List<(string Path, byte[] Data)> items;
    private readonly StrategyBasedComWrappers comWrappers;
    private readonly string? password;
    private readonly IProgress<ArchiveProgress>? progress;
    private readonly CancellationToken cancellationToken;
    private ulong totalBytes;
    private readonly List<object> liveObjects = new();

    public UpdateCallback(Dictionary<string, byte[]> files, StrategyBasedComWrappers cw,
            string? password = null,
            IProgress<ArchiveProgress>? progress = null,
            CancellationToken cancellationToken = default)
    {
        this.items = files.Select(kv => (kv.Key, kv.Value)).ToList();
        this.comWrappers = cw;
        this.password = password;
        this.progress = progress;
        this.cancellationToken = cancellationToken;
    }

    // IProgress
    public int SetTotal(ulong total)
    {
        this.totalBytes = total;
        return 0;
    }

    public int SetCompleted(nint completeValue)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return unchecked((int)0x80004004); // E_ABORT
        }

        if (this.progress != null && completeValue != nint.Zero)
        {
            unsafe
            {
                ulong completed = *(ulong*)completeValue;
                this.progress.Report(new ArchiveProgress(this.totalBytes, completed));
            }
        }
        return 0;
    }

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
        var (path, data) = this.items[(int)index];
        long now = DateTime.UtcNow.ToFileTimeUtc();
        var info = new ArchiveItemProperties(path, (ulong)data.Length,
                0x20, now, now, now);
        ArchiveItemProperties.FillProperty(propID, ref value, info);
        return 0;
    }

    public int GetStream(uint index, out nint inStream)
    {
        var data = this.items[(int)index].Data;
        var ms = new MemoryStream(data, writable: false);
        var wrapper = new InStreamWrapper(ms);
        this.liveObjects.Add(ms);
        this.liveObjects.Add(wrapper);

        nint ccw = this.comWrappers.GetOrCreateComInterfaceForObject(wrapper, CreateComInterfaceFlags.None);
        Guid iid = Iid.ISequentialInStream;
        Marshal.QueryInterface(ccw, ref iid, out inStream);
        Marshal.Release(ccw);
        return 0;
    }

    public int SetOperationResult(int operationResult) => 0;

    public int CryptoGetTextPassword2(out int passwordIsDefined, out nint password)
    {
        if (this.password != null)
        {
            passwordIsDefined = 1;
            password = Marshal.StringToBSTR(this.password);
        }
        else
        {
            passwordIsDefined = 0;
            password = nint.Zero;
        }
        return 0;
    }
}

/// <summary>
/// Archive creation callback that streams files from disk instead of memory.
/// </summary>
[GeneratedComClass]
public partial class FileUpdateCallback : IArchiveUpdateCallback, ICryptoGetTextPassword2
{
    private readonly List<(string ArchiveName, string FilePath)> items;
    private readonly StrategyBasedComWrappers comWrappers;
    private readonly string? password;
    private readonly IProgress<ArchiveProgress>? progress;
    private readonly CancellationToken cancellationToken;
    private ulong totalBytes;
    private readonly List<object> liveObjects = new();
    private FileStream? currentFileStream;

    public FileUpdateCallback(Dictionary<string, string> files, StrategyBasedComWrappers cw,
            string? password = null,
            IProgress<ArchiveProgress>? progress = null,
            CancellationToken cancellationToken = default)
    {
        this.items = files.Select(kv => (kv.Key, kv.Value)).ToList();
        this.comWrappers = cw;
        this.password = password;
        this.progress = progress;
        this.cancellationToken = cancellationToken;
    }

    // IProgress
    public int SetTotal(ulong total)
    {
        this.totalBytes = total;
        return 0;
    }

    public int SetCompleted(nint completeValue)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return unchecked((int)0x80004004); // E_ABORT
        }

        if (this.progress != null && completeValue != nint.Zero)
        {
            unsafe
            {
                ulong completed = *(ulong*)completeValue;
                this.progress.Report(new ArchiveProgress(this.totalBytes, completed));
            }
        }
        return 0;
    }

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
        var (archiveName, filePath) = this.items[(int)index];
        var fileInfo = new FileInfo(filePath);
        var info = new ArchiveItemProperties(archiveName, (ulong)fileInfo.Length,
                (uint)fileInfo.Attributes,
                fileInfo.CreationTimeUtc.ToFileTimeUtc(),
                fileInfo.LastAccessTimeUtc.ToFileTimeUtc(),
                fileInfo.LastWriteTimeUtc.ToFileTimeUtc());
        ArchiveItemProperties.FillProperty(propID, ref value, info);
        return 0;
    }

    public int GetStream(uint index, out nint inStream)
    {
        var filePath = this.items[(int)index].FilePath;
        var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        this.currentFileStream = fs;
        var wrapper = new InStreamWrapper(fs);
        this.liveObjects.Add(wrapper);

        nint ccw = this.comWrappers.GetOrCreateComInterfaceForObject(wrapper, CreateComInterfaceFlags.None);
        Guid iid = Iid.ISequentialInStream;
        Marshal.QueryInterface(ccw, ref iid, out inStream);
        Marshal.Release(ccw);
        return 0;
    }

    public int SetOperationResult(int operationResult)
    {
        if (this.currentFileStream != null)
        {
            this.currentFileStream.Dispose();
            this.currentFileStream = null;
        }
        return 0;
    }

    public int CryptoGetTextPassword2(out int passwordIsDefined, out nint password)
    {
        if (this.password != null)
        {
            passwordIsDefined = 1;
            password = Marshal.StringToBSTR(this.password);
        }
        else
        {
            passwordIsDefined = 0;
            password = nint.Zero;
        }
        return 0;
    }
}

/// <summary>
/// Extraction callback that writes each item directly to files on disk.
/// </summary>
[GeneratedComClass]
public partial class DirectoryExtractCallback : IArchiveExtractCallback, ICryptoGetTextPassword
{
    private readonly IInArchive archive;
    private readonly StrategyBasedComWrappers comWrappers;
    private readonly string baseDirectory;
    private readonly string? password;
    private readonly IProgress<ArchiveProgress>? progress;
    private readonly CancellationToken cancellationToken;
    private ulong totalBytes;
    private uint currentIndex;
    private FileStream? currentFileStream;
    private readonly List<object> liveObjects = new();

    /// <summary>Failures collected during extraction.</summary>
    public List<ExtractionFailure> Failures { get; } = new();

    public DirectoryExtractCallback(IInArchive archive, StrategyBasedComWrappers cw,
            string baseDirectory, string? password = null,
            IProgress<ArchiveProgress>? progress = null,
            CancellationToken cancellationToken = default)
    {
        this.archive = archive;
        this.comWrappers = cw;
        this.baseDirectory = Path.GetFullPath(baseDirectory) + Path.DirectorySeparatorChar;
        this.password = password;
        this.progress = progress;
        this.cancellationToken = cancellationToken;
    }

    // IProgress
    public int SetTotal(ulong total)
    {
        this.totalBytes = total;
        return 0;
    }

    public int SetCompleted(nint completeValue)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return unchecked((int)0x80004004); // E_ABORT
        }

        if (this.progress != null && completeValue != nint.Zero)
        {
            unsafe
            {
                ulong completed = *(ulong*)completeValue;
                this.progress.Report(new ArchiveProgress(this.totalBytes, completed));
            }
        }
        return 0;
    }

    // IArchiveExtractCallback
    public int GetStream(uint index, out nint outStream, int askExtractMode)
    {
        this.currentIndex = index;
        this.currentFileStream = null;
        outStream = nint.Zero;

        // Only create output stream for actual extraction (not test/skip)
        if (askExtractMode != 0) // 0 = kExtract
        {
            return 0;
        }

        // Get the path property
        PropVariant pvPath = default;
        this.archive.GetProperty(index, PropId.kpidPath, ref pvPath);
        string? itemPath = pvPath.GetBstr();
        NativeMethods.PropVariantClear(ref pvPath);

        if (string.IsNullOrEmpty(itemPath))
        {
            return 0;
        }

        // Check if it's a directory
        PropVariant pvDir = default;
        this.archive.GetProperty(index, PropId.kpidIsDir, ref pvDir);
        bool isDir = pvDir.GetBool();

        if (isDir)
        {
            string dirPath = this.ValidatePath(itemPath);
            Directory.CreateDirectory(dirPath);
            return 0;
        }

        // It's a file — create parent directories and open a FileStream
        string fullPath = this.ValidatePath(itemPath);
        string? parentDir = Path.GetDirectoryName(fullPath);
        if (parentDir != null)
        {
            Directory.CreateDirectory(parentDir);
        }

        this.currentFileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        var wrapper = new OutStreamWrapper(this.currentFileStream);
        this.liveObjects.Add(wrapper);

        nint ccw = this.comWrappers.GetOrCreateComInterfaceForObject(wrapper, CreateComInterfaceFlags.None);
        Guid iid = Iid.ISequentialOutStream;
        Marshal.QueryInterface(ccw, ref iid, out outStream);
        Marshal.Release(ccw);
        return 0;
    }

    public int PrepareOperation(int askExtractMode) => 0;

    public int SetOperationResult(int opRes)
    {
        if (this.currentFileStream != null)
        {
            this.currentFileStream.Dispose();
            this.currentFileStream = null;
        }

        if (opRes != 0)
        {
            this.Failures.Add(new ExtractionFailure(this.currentIndex, (ExtractionResult)opRes));
        }

        return 0;
    }

    public int CryptoGetTextPassword(out nint password)
    {
        if (this.password == null)
        {
            password = nint.Zero;
            return unchecked((int)0x80004004); // E_ABORT
        }
        password = Marshal.StringToBSTR(this.password);
        return 0;
    }

    private string ValidatePath(string itemPath)
    {
        return ValidatePathInternal(this.baseDirectory, itemPath);
    }

    internal static string ValidatePathInternal(string baseDirectory, string itemPath)
    {
        string fullPath = Path.GetFullPath(Path.Combine(baseDirectory, itemPath));
        if (!fullPath.StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Entry path '{itemPath}' resolves outside the target directory.");
        }
        return fullPath;
    }
}

/// <summary>
/// Shared item metadata for archive creation callbacks.
/// Used by both in-memory and disk-based update callbacks to avoid
/// duplicating the property-filling switch statement.
/// </summary>
internal readonly record struct ArchiveItemProperties(
    string Name, ulong Size, uint Attributes,
    long CreationTime, long AccessTime, long ModifiedTime)
{
    public static void FillProperty(uint propID, ref PropVariant value,
            ArchiveItemProperties item)
    {
        switch (propID)
        {
            case PropId.kpidPath:
                value.VarType = PropVariant.VT_BSTR;
                value.PointerValue = Marshal.StringToBSTR(item.Name);
                break;
            case PropId.kpidIsDir:
                value.VarType = PropVariant.VT_BOOL;
                value.BoolValue = 0;
                break;
            case PropId.kpidSize:
                value.VarType = PropVariant.VT_UI8;
                value.ULongValue = item.Size;
                break;
            case PropId.kpidAttrib:
                value.VarType = PropVariant.VT_UI4;
                value.UIntValue = item.Attributes;
                break;
            case PropId.kpidCTime:
                value.VarType = PropVariant.VT_FILETIME;
                value.LongValue = item.CreationTime;
                break;
            case PropId.kpidATime:
                value.VarType = PropVariant.VT_FILETIME;
                value.LongValue = item.AccessTime;
                break;
            case PropId.kpidMTime:
                value.VarType = PropVariant.VT_FILETIME;
                value.LongValue = item.ModifiedTime;
                break;
            case 21: // kpidIsAnti
                value.VarType = PropVariant.VT_BOOL;
                value.BoolValue = 0;
                break;
        }
    }
}
