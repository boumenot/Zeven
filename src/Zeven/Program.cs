using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Zeven.Core;
using Zeven.Core.Interop;

#pragma warning disable CS9191 // Marshal.QueryInterface takes ref Guid, not in Guid

// ── Configuration ───────────────────────────────────────────────────────────

const string DllPath = @"q:\7z2601-bin\x64\7z.dll";
const string ExePath = @"q:\7z2601-bin\x64\7za.exe";

// 7z format CLSID: {23170F69-40C1-278A-1000-000110070000}
Guid clsid7z = new("23170F69-40C1-278A-1000-000110070000");
// IInArchive IID
Guid iidInArchive = new("23170F69-40C1-278A-0000-000600600000");

// ── Load native DLL and resolve exports ─────────────────────────────────────

Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("  7-Zip Registration-Free COM Interop Demo (.NET 10)");
Console.WriteLine("  Source-generated COM wrappers via [GeneratedComInterface]");
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine();

nint lib = NativeLibrary.Load(DllPath);
Console.WriteLine($"✓ Loaded {DllPath}");

var createObject = Marshal.GetDelegateForFunctionPointer<CreateObjectFunc>(
    NativeLibrary.GetExport(lib, "CreateObject"));
var getNumberOfFormats = Marshal.GetDelegateForFunctionPointer<GetNumberOfFormatsFunc>(
    NativeLibrary.GetExport(lib, "GetNumberOfFormats"));
var getHandlerProperty2 = Marshal.GetDelegateForFunctionPointer<GetHandlerProperty2Func>(
    NativeLibrary.GetExport(lib, "GetHandlerProperty2"));

Console.WriteLine("✓ Resolved CreateObject, GetNumberOfFormats, GetHandlerProperty2");
Console.WriteLine();

// ── Enumerate supported archive formats ─────────────────────────────────────

getNumberOfFormats(out uint numFormats);
Console.WriteLine($"Supported archive formats ({numFormats}):");
for (uint i = 0; i < numFormats; i++)
{
    PropVariant pv = default;

    getHandlerProperty2(i, HandlerPropId.kName, ref pv);
    string? name = pv.GetBstr();
    NativeMethods.PropVariantClear(ref pv);

    pv = default;
    getHandlerProperty2(i, HandlerPropId.kExtension, ref pv);
    string? ext = pv.GetBstr();
    NativeMethods.PropVariantClear(ref pv);

    pv = default;
    getHandlerProperty2(i, HandlerPropId.kUpdate, ref pv);
    bool canUpdate = pv.GetBool();
    NativeMethods.PropVariantClear(ref pv);

    Console.WriteLine($"  {name,-12} *.{ext,-16} {(canUpdate ? "[read/write]" : "[read-only]")}");
}
Console.WriteLine();

// ── Create a test archive ───────────────────────────────────────────────────

string tempDir = Path.Combine(Path.GetTempPath(), "7zcs_test_" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(tempDir);
string archivePath;

if (args.Length > 0 && File.Exists(args[0]))
{
    archivePath = Path.GetFullPath(args[0]);
    Console.WriteLine($"Using existing archive: {archivePath}");
}
else
{
    // Create sample files
    File.WriteAllText(Path.Combine(tempDir, "hello.txt"), "Hello, World! This is a test file for 7-Zip COM Interop.");
    File.WriteAllText(Path.Combine(tempDir, "readme.md"), "# 7-Zip COM Interop Test\n\nThis archive was created to verify .NET source-generated COM bindings.\n");
    File.WriteAllText(Path.Combine(tempDir, "data.csv"), "Name,Value\nAlpha,1\nBeta,2\nGamma,3\n");

    archivePath = Path.Combine(tempDir, "test.7z");
    var psi = new ProcessStartInfo(ExePath)
    {
        Arguments = $"a -t7z \"{archivePath}\" \"{Path.Combine(tempDir, "hello.txt")}\" \"{Path.Combine(tempDir, "readme.md")}\" \"{Path.Combine(tempDir, "data.csv")}\"",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    var proc = Process.Start(psi)!;
    proc.WaitForExit();

    if (proc.ExitCode != 0 || !File.Exists(archivePath))
    {
        Console.Error.WriteLine("Failed to create test archive with 7za.exe");
        return 1;
    }
    Console.WriteLine($"✓ Created test archive: {archivePath} ({new FileInfo(archivePath).Length} bytes)");
}
Console.WriteLine();

// ── Create IInArchive via COM ───────────────────────────────────────────────

int hr = createObject(in clsid7z, in iidInArchive, out nint archivePtr);
if (hr != 0)
{
    Console.Error.WriteLine($"CreateObject failed: 0x{hr:X8}");
    return 1;
}
Console.WriteLine("✓ Created IInArchive COM object for 7z format");

var comWrappers = new StrategyBasedComWrappers();
var archive = (IInArchive)comWrappers.GetOrCreateObjectForComInstance(
    archivePtr, CreateObjectFlags.UniqueInstance);
Console.WriteLine("✓ Wrapped native COM pointer with [GeneratedComInterface] proxy");

// ── Open the archive ────────────────────────────────────────────────────────

using var fileStream = File.OpenRead(archivePath);
var inStream = new InStreamWrapper(fileStream);
var openCallback = new ArchiveOpenCallback();

// Create COM Callable Wrappers (CCW) for managed objects and QI for the right interface
nint streamCcw = comWrappers.GetOrCreateComInterfaceForObject(inStream, CreateComInterfaceFlags.None);
Guid iidInStream = new("23170F69-40C1-278A-0000-000300030000");
Marshal.QueryInterface(streamCcw, ref iidInStream, out nint streamPtr);

nint callbackCcw = comWrappers.GetOrCreateComInterfaceForObject(openCallback, CreateComInterfaceFlags.None);
Guid iidCallback = new("23170F69-40C1-278A-0000-000600100000");
Marshal.QueryInterface(callbackCcw, ref iidCallback, out nint callbackPtr);

unsafe
{
    ulong scanSize = 1 << 23;
    hr = archive.Open(streamPtr, (nint)(&scanSize), callbackPtr);
}

// Release our QI references (archive holds its own via AddRef)
if (streamPtr != nint.Zero) Marshal.Release(streamPtr);
if (callbackPtr != nint.Zero) Marshal.Release(callbackPtr);
Marshal.Release(streamCcw);
Marshal.Release(callbackCcw);

if (hr != 0)
{
    Console.Error.WriteLine($"IInArchive::Open failed: 0x{hr:X8}");
    return 1;
}
Console.WriteLine("✓ Archive opened successfully via COM interop");
Console.WriteLine();

// ── List archive contents ───────────────────────────────────────────────────

archive.GetNumberOfItems(out uint numItems);
Console.WriteLine($"Archive contents ({numItems} items):");
Console.WriteLine($"  {"Path",-35} {"Size",10} {"Packed",10} {"Modified",-20} {"Dir",4}");
Console.WriteLine("  " + new string('─', 85));

for (uint i = 0; i < numItems; i++)
{
    PropVariant pv = default;

    // Path
    archive.GetProperty(i, PropId.kpidPath, ref pv);
    string path = pv.GetBstr() ?? "(unnamed)";
    NativeMethods.PropVariantClear(ref pv);

    // Size
    pv = default;
    archive.GetProperty(i, PropId.kpidSize, ref pv);
    ulong size = pv.GetUInt64();

    // Packed size
    pv = default;
    archive.GetProperty(i, PropId.kpidPackSize, ref pv);
    ulong packedSize = pv.GetUInt64();

    // IsDir
    pv = default;
    archive.GetProperty(i, PropId.kpidIsDir, ref pv);
    bool isDir = pv.GetBool();

    // Modified time
    pv = default;
    archive.GetProperty(i, PropId.kpidMTime, ref pv);
    DateTime? mtime = pv.GetFileTime();

    Console.WriteLine($"  {path,-35} {size,10:N0} {packedSize,10:N0} {mtime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",-20} {(isDir ? " Yes" : "")}");
}

// ── Read archive-level properties ───────────────────────────────────────────

Console.WriteLine();
Console.WriteLine("Archive properties:");

PropVariant apv = default;
archive.GetArchiveProperty(PropId.kpidMethod, ref apv);
string? method = apv.GetBstr();
NativeMethods.PropVariantClear(ref apv);
if (method != null) Console.WriteLine($"  Method:    {method}");

apv = default;
archive.GetArchiveProperty(PropId.kpidSize, ref apv);
ulong totalSize = apv.GetUInt64();
if (totalSize > 0) Console.WriteLine($"  Total size: {totalSize:N0} bytes");

// ── Cleanup ─────────────────────────────────────────────────────────────────

archive.Close();
Console.WriteLine();
Console.WriteLine("✓ Archive closed");

if (args.Length == 0)
{
    try { Directory.Delete(tempDir, true); } catch { }
}

Console.WriteLine("✓ Done — registration-free COM interop with 7z.dll works!");
return 0;
