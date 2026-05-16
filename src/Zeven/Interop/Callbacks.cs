using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Zeven;

#pragma warning disable CS9191 // Marshal.QueryInterface takes ref Guid, not in Guid

namespace Zeven.Interop;

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
    private string? currentPath;
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
                this.progress.Report(new ArchiveProgress(this.totalBytes, completed, this.currentPath, this.currentIndex));
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

        // Capture current entry path for progress reporting
        PropVariant pvPath = default;
        this.archive.GetProperty(index, PropId.kpidPath, ref pvPath);
        this.currentPath = pvPath.GetBstr();
        NativeMethods.PropVariantClear(ref pvPath);

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
/// Extraction callback that writes a single entry directly to a caller-provided stream.
/// No memory buffering — data flows from the archive decoder to the output stream.
/// </summary>
[GeneratedComClass]
public partial class StreamTargetExtractCallback : IArchiveExtractCallback, ICryptoGetTextPassword
{
    private readonly IInArchive archive;
    private readonly StrategyBasedComWrappers comWrappers;
    private readonly uint targetIndex;
    private readonly Stream output;
    private readonly string? password;
    private readonly IProgress<ArchiveProgress>? progress;
    private readonly CancellationToken cancellationToken;
    private ulong totalBytes;
    private string? currentPath;
    private readonly List<object> liveObjects = new();

    public List<ExtractionFailure> Failures { get; } = new();

    public StreamTargetExtractCallback(IInArchive archive, StrategyBasedComWrappers cw,
            uint targetIndex, Stream output, string? password = null,
            IProgress<ArchiveProgress>? progress = null,
            CancellationToken cancellationToken = default)
    {
        this.archive = archive;
        this.comWrappers = cw;
        this.targetIndex = targetIndex;
        this.output = output;
        this.password = password;
        this.progress = progress;
        this.cancellationToken = cancellationToken;
    }

    public int SetTotal(ulong total)
    {
        this.totalBytes = total;
        return 0;
    }

    public int SetCompleted(nint completeValue)
    {
        if (this.cancellationToken.IsCancellationRequested)
        {
            return unchecked((int)0x80004004);
        }

        if (this.progress != null && completeValue != nint.Zero)
        {
            unsafe
            {
                ulong completed = *(ulong*)completeValue;
                this.progress.Report(new ArchiveProgress(this.totalBytes, completed, this.currentPath, this.targetIndex));
            }
        }
        return 0;
    }

    public int GetStream(uint index, out nint outStream, int askExtractMode)
    {
        outStream = nint.Zero;

        if (askExtractMode != 0 || index != this.targetIndex)
        {
            return 0;
        }

        // Capture current entry path for progress reporting
        PropVariant pvPath = default;
        this.archive.GetProperty(index, PropId.kpidPath, ref pvPath);
        this.currentPath = pvPath.GetBstr();
        NativeMethods.PropVariantClear(ref pvPath);

        var wrapper = new OutStreamWrapper(this.output);
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
        if (opRes != 0)
        {
            this.Failures.Add(new ExtractionFailure(this.targetIndex, (ExtractionResult)opRes));
        }
        return 0;
    }

    public int CryptoGetTextPassword(out nint password)
    {
        if (this.password == null)
        {
            password = nint.Zero;
            return unchecked((int)0x80004004);
        }
        password = Marshal.StringToBSTR(this.password);
        return 0;
    }
}

/// <summary>
/// Abstract base for archive creation callbacks.
/// Consolidates shared progress, cancellation, password, and COM stream logic.
/// Concrete subclasses provide item lookup, property filling, and stream creation.
/// </summary>
public abstract class UpdateCallbackBase : IArchiveUpdateCallback, ICryptoGetTextPassword2
{
    private readonly StrategyBasedComWrappers comWrappers;
    private readonly string? password;
    private readonly IProgress<ArchiveProgress>? progress;
    private readonly CancellationToken cancellationToken;
    private ulong totalBytes;
    private readonly List<object> liveObjects = new();

    protected UpdateCallbackBase(StrategyBasedComWrappers cw, string? password = null,
            IProgress<ArchiveProgress>? progress = null,
            CancellationToken cancellationToken = default)
    {
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

    public virtual int SetOperationResult(int operationResult) => 0;

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

    public abstract int GetUpdateItemInfo(uint index, out int newData, out int newProps,
            out uint indexInArchive);

    public abstract int GetProperty(uint index, uint propID, ref PropVariant value);

    public abstract int GetStream(uint index, out nint inStream);

    protected int CreateInStream(Stream stream, out nint inStream)
    {
        var wrapper = new InStreamWrapper(stream);
        this.liveObjects.Add(stream);
        this.liveObjects.Add(wrapper);

        nint ccw = this.comWrappers.GetOrCreateComInterfaceForObject(wrapper,
                CreateComInterfaceFlags.None);
        Guid iid = Iid.ISequentialInStream;
        Marshal.QueryInterface(ccw, ref iid, out inStream);
        Marshal.Release(ccw);
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
    private string? currentPath;
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
        this.baseDirectory = Path.GetFullPath(baseDirectory).TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
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
                this.progress.Report(new ArchiveProgress(this.totalBytes, completed, this.currentPath, this.currentIndex));
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
        this.currentPath = itemPath;
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

    public static string ValidatePathInternal(string baseDirectory, string itemPath)
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
            case PropId.kpidIsAnti:
                value.VarType = PropVariant.VT_BOOL;
                value.BoolValue = 0;
                break;
        }
    }
}

/// <summary>
/// Describes a single item in the merged output for archive update operations.
/// </summary>
internal record MergeItem
{
    public uint? SourceIndex { get; init; }
    public string? Path { get; init; }
    public object? DataSource { get; init; }
    public long? Size { get; init; }
    public bool IsNew { get; init; }
}

/// <summary>
/// Unified archive callback for both creation and update operations.
/// Handles copy (unchanged), new, and replace semantics for 7-Zip's IArchiveUpdateCallback.
/// When sourceArchive is null, all items are treated as new (archive creation).
/// </summary>
[GeneratedComClass]
internal partial class MergeUpdateCallback : UpdateCallbackBase
{
    private readonly IInArchive? sourceArchive;
    private readonly List<MergeItem> outputItems;
    private FileStream? currentFileStream;

    public MergeUpdateCallback(IInArchive? sourceArchive, StrategyBasedComWrappers cw,
            List<MergeItem> outputItems,
            string? password = null,
            IProgress<ArchiveProgress>? progress = null,
            CancellationToken cancellationToken = default)
        : base(cw, password, progress, cancellationToken)
    {
        this.sourceArchive = sourceArchive;
        this.outputItems = outputItems;
    }

    public override int GetUpdateItemInfo(uint index, out int newData, out int newProps,
            out uint indexInArchive)
    {
        var item = this.outputItems[(int)index];

        if (item.DataSource != null || item.IsNew)
        {
            newData = 1;
            newProps = 1;
            indexInArchive = item.SourceIndex ?? unchecked((uint)-1);
        }
        else
        {
            newData = 0;
            newProps = 0;
            indexInArchive = item.SourceIndex!.Value;
        }
        return 0;
    }

    public override int GetProperty(uint index, uint propID, ref PropVariant value)
    {
        value = default;
        var item = this.outputItems[(int)index];

        if (item.DataSource != null || item.IsNew)
        {
            long ctime, atime, mtime;
            uint attrs;
            ulong size;

            if (item.DataSource is string filePath)
            {
                var fileInfo = new FileInfo(filePath);
                size = (ulong)fileInfo.Length;
                attrs = (uint)fileInfo.Attributes;
                ctime = fileInfo.CreationTimeUtc.ToFileTimeUtc();
                atime = fileInfo.LastAccessTimeUtc.ToFileTimeUtc();
                mtime = fileInfo.LastWriteTimeUtc.ToFileTimeUtc();
            }
            else
            {
                size = (ulong)(item.Size ?? 0);
                attrs = 0x20;
                long now = DateTime.UtcNow.ToFileTimeUtc();
                ctime = now;
                atime = now;
                mtime = now;
            }

            var info = new ArchiveItemProperties(item.Path!, size, attrs,
                    ctime, atime, mtime);
            ArchiveItemProperties.FillProperty(propID, ref value, info);
        }
        else
        {
            this.sourceArchive!.GetProperty(item.SourceIndex!.Value, propID, ref value);
        }
        return 0;
    }

    public override int GetStream(uint index, out nint inStream)
    {
        var item = this.outputItems[(int)index];
        inStream = nint.Zero;

        if (item.DataSource == null)
        {
            return 0;
        }

        switch (item.DataSource)
        {
            case byte[] bytes:
                return this.CreateInStream(new MemoryStream(bytes, writable: false),
                        out inStream);
            case string filePath:
            {
                var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                this.currentFileStream = fs;
                return this.CreateInStream(fs, out inStream);
            }
            case Stream stream:
                return this.CreateInStream(stream, out inStream);
            default:
                return 0;
        }
    }

    public override int SetOperationResult(int operationResult)
    {
        if (this.currentFileStream != null)
        {
            this.currentFileStream.Dispose();
            this.currentFileStream = null;
        }
        return 0;
    }
}
