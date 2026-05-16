# Zeven

A .NET 10 registration-free COM interop wrapper for 7-Zip's native DLLs, using source-generated COM interfaces (`[GeneratedComInterface]` / `[GeneratedComClass]`).

No COM registration, no IDL, no type libraries — just P/Invoke `CreateObject` + source-generated vtable proxies.

## Prerequisites

- **.NET 10 SDK**
- **7z.dll** — either from stock [7-Zip](https://7-zip.org/) or from [7-Zip-zstd](https://github.com/mcmilk/7-Zip-zstd) (recommended). Stock 7-Zip supports all archive formats and built-in codecs (LZMA, LZMA2, PPMd, BZip2, Deflate). 7-Zip-zstd is a fork that adds **Zstd, Brotli, LZ4, LZ5, Lizard, and Fast LZMA2** codecs. CI tests run against the 7-Zip-zstd build.

## Quick Start

### Batch compress and decompress

```csharp
using Zeven.Core;

// Load the 7-Zip native DLL
ZevenLibrary.Load(@"path\to\7z.dll");

// Batch compress/decompress
using var compressed = new MemoryStream();
ZstdCodec.Compress(inputStream, compressed);

compressed.Position = 0;
using var decompressed = new MemoryStream();
ZstdCodec.Decompress(compressed, decompressed);
```

### Streaming compress and decompress

```csharp
using System.IO.Compression;
using Zeven.Core;

// Streaming compress
using (var compressor = new ZstdStream(outputFile, CompressionMode.Compress))
{
    compressor.Write(data);
}

// Streaming decompress
using var decompressor = new ZstdStream(inputFile, CompressionMode.Decompress);
decompressor.CopyTo(result);
```

### Custom compression options

```csharp
// Custom options
var options = new ZstdOptions { Level = 9, ChunkSize = 4 * 1024 * 1024 };
using var compressor = new ZstdStream(output, CompressionMode.Compress, options);
```

All codecs (LZMA2, PPMd, Zstd, Brotli, LZ4) follow the same API pattern — swap `Zstd` for any codec name.

### Create and extract a .7z archive

```csharp
using Zeven.Core;

var lib = ZevenLibrary.Load(@"path\to\7z.dll");

// Create a .7z archive from files on disk (no memory buffering)
var files = new Dictionary<string, string>
{
    ["report.pdf"] = @"C:\docs\report.pdf",
    ["data.csv"]   = @"C:\docs\data.csv",
};

using var archive = File.Create(@"C:\docs\backup.7z");
lib.CreateArchive("7z", archive, files);

// Extract to a directory
using var input = File.OpenRead(@"C:\docs\backup.7z");
using var handle = lib.OpenArchive("7z", input);
handle.ExtractTo(@"C:\output");
```

### In-memory archives

An in-memory API is also available for small archives:

```csharp
// Create from byte arrays
var files = new Dictionary<string, byte[]>
{
    ["hello.txt"] = "Hello, World!"u8.ToArray(),
};
using var output = new MemoryStream();
lib.CreateArchive("7z", output, files);

// Read back
output.Position = 0;
using var handle = lib.OpenArchive("7z", output);
var extracted = handle.ExtractAll(); // Dictionary<string, byte[]>
```

### Creating a .tar.zst archive

Tar + Zstd is a two-step process: create the tar container, then compress with Zstd.

```csharp
using Zeven.Core;

var lib = ZevenLibrary.Load(@"path\to\7z.dll");

// Step 1: Create a .tar archive in memory
var files = new Dictionary<string, string>
{
    ["src/main.cs"] = @"C:\project\src\main.cs",
    ["README.md"]   = @"C:\project\README.md",
};

using var tarStream = new MemoryStream();
lib.CreateArchive("tar", tarStream, files);

// Step 2: Compress with Zstd
tarStream.Position = 0;
using var output = File.Create(@"C:\project\backup.tar.zst");
ZstdCodec.Compress(tarStream, output, new ZstdOptions { Level = 3 });
```

To decompress and extract:

```csharp
// Step 1: Decompress Zstd
using var zstInput = File.OpenRead(@"C:\project\backup.tar.zst");
using var tarStream = new MemoryStream();
ZstdCodec.Decompress(zstInput, tarStream);

// Step 2: Extract tar
tarStream.Position = 0;
using var handle = lib.OpenArchive("tar", tarStream);
handle.ExtractTo(@"C:\output");
```

### Listing, inspecting, and extracting a single file

```csharp
using Zeven.Core;

var lib = ZevenLibrary.Load(@"path\to\7z.dll");
using var handle = lib.OpenArchive("7z", File.OpenRead(@"C:\docs\backup.7z"));

// List all entries
foreach (var entry in handle.Entries)
{
    Console.WriteLine($"  {entry.Path} ({entry.Size:N0} bytes)");
}

// Find a specific file and read its metadata
var report = handle.Entries.First(e => e.Path == "report.pdf");
Console.WriteLine($"Size: {report.Size:N0} bytes");
Console.WriteLine($"Modified: {report.ModifiedTime}");
Console.WriteLine($"Compressed: {report.PackedSize:N0} bytes");

// Extract just that one file
File.WriteAllBytes(@"C:\output\report.pdf", handle.Extract("report.pdf"));

// Stream directly to a file — no memory buffering
using var outputFile = File.Create(@"C:\output\large-log.txt");
handle.ExtractTo("logs/large-log.txt", outputFile);
```

### Archive creation options

Control compression level, method, threading, and encryption per format:

```csharp
// 7z with LZMA2 level 9, encrypted headers
lib.CreateArchive("7z", output, files, new SevenZipCreateOptions
{
    Level = 9,
    Method = "LZMA2",
    Solid = true,
    EncryptHeaders = true,
    NumThreads = 4,
}, password: "secret");

// Zip with Deflate
lib.CreateArchive("zip", output, files, new ZipCreateOptions
{
    Level = 6,
    Method = "Deflate",
});

// GZip, BZip2, xz
lib.CreateArchive("gzip", output, files, new GZipCreateOptions { Level = 9 });
lib.CreateArchive("bzip2", output, files, new BZip2CreateOptions { Level = 5, NumPasses = 7 });
lib.CreateArchive("xz", output, files, new XzCreateOptions { Level = 6, NumThreads = 2 });
```

Each format has its own typed options class implementing `IArchiveCreateOptions`.

### Updating existing archives

Add, replace, or delete entries in an existing archive:

```csharp
using var handle = lib.OpenArchive("7z", File.OpenRead(@"C:\docs\backup.7z"));

using var updated = File.Create(@"C:\docs\backup-updated.7z");
lib.UpdateArchive("7z", handle, updated, u => u
    .Add("new-report.pdf", @"C:\docs\new-report.pdf")
    .Replace("config.json", updatedConfigBytes)
    .Delete("old-log.txt"));
```

The builder supports `byte[]`, file path, and `Stream` sources:

```csharp
lib.UpdateArchive("7z", handle, output, update =>
{
    update.Add("from-memory.txt", data);
    update.Add("from-disk.txt", @"C:\path\to\file.txt");
    update.Add("from-stream.bin", inputStream, size: streamLength);
    update.Replace("existing.txt", newData);
    update.Delete("unwanted.txt");
});
```

> **Note:** 7-Zip always writes a complete new archive — it cannot modify in-place. The source archive must remain open during the update.

### Progress and cancellation

All archive operations support progress reporting and cancellation:

```csharp
var progress = new Progress<ArchiveProgress>(p =>
    Console.WriteLine($"{p.CompletedBytes / 1024}KB / {p.TotalBytes / 1024}KB"));

var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

// Extract with progress and timeout
handle.ExtractTo(@"C:\output", progress, cts.Token);

// Create with progress
lib.CreateArchive("7z", output, files, progress: progress);

// Update with cancellation
lib.UpdateArchive("7z", handle, output, u => u.Add("f.txt", data),
    cancellationToken: cts.Token);
```

## 7-Zip Native DLLs

7-Zip ships several DLLs built from different source bundles. They all export the same COM factory function (`CreateObject`) but differ in which archive formats and codecs are compiled in.

### DLL Comparison

| DLL | Source Bundle | Formats | Codecs | Read | Write | Size (x64) |
|---|---|---|---|---|---|---|
| **`7z.dll`** | `Format7zF` | **All ~60** (7z, zip, tar, gz, xz, cab, iso, wim, ext, ntfs, …) | All (LZMA, LZMA2, PPMd, BZip2, Deflate, zstd, …) | ✅ | ✅ | ~1.9 MB |
| `7za.dll` | `Format7z` | **7z only** | All | ✅ | ✅ | ~1.3 MB |
| `7zxa.dll` | `Format7zExtract` | **7z only** | Decoders only | ✅ | ❌ | ~0.7 MB |
| `7zra.dll` | `Format7zR` | **7z only** | Reduced set | ✅ | ✅ | ~1.0 MB |
| `7zxr.dll` | `Format7zExtractR` | **7z only** | Reduced decoders only | ✅ | ❌ | ~0.5 MB |

### Which DLL to use?

- **`7z.dll`** — Use this for full functionality. Supports reading and writing all formats. Ships with the 7-Zip installer (`C:\Program Files\7-Zip\7z.dll`). **This is what Zeven uses by default.**

- **`7za.dll`** — Use for minimal deployments that only need the 7z format. Available in the [7-Zip Extra](https://7-zip.org/download.html) standalone package.

- **`7zxa.dll`** — Use when you only need to extract 7z archives and want the smallest possible binary. Also in the standalone package.

- **`7zra.dll` / `7zxr.dll`** — Reduced codec variants. Smaller but support fewer compression methods.

### Formats vs Codecs

7-Zip has two distinct concepts that are easy to confuse:

- A **format** (handler) is a container — it knows how to parse and write a specific archive structure (e.g., `.7z`, `.zip`, `.tar`). Each format handler is a COM object created via `CreateObject` with a format-specific CLSID. Formats report whether they support writing via `GetHandlerProperty2(kUpdate)`.

- A **codec** is a compression/encryption algorithm (e.g., LZMA2, Deflate, PPMd, AES). Codecs are used *inside* format handlers. A format like `.7z` can use many different codecs.

This distinction matters because a name like "PPMd" or "LZMA" can refer to *either* a standalone container format *or* a compression codec used inside another format — and the read/write support differs:

| Name | As a standalone **format** | Format extensions | As a **codec** inside `.7z` |
|---|---|---|---|
| 7z | ✅ Read / ✅ Write | `.7z` | — (this *is* the container) |
| Zip | ✅ Read / ✅ Write | `.zip .jar .docx .xlsx .epub` | — |
| Tar | ✅ Read / ✅ Write | `.tar .ova` | — |
| GZip | ✅ Read / ✅ Write | `.gz .tgz` | — |
| BZip2 | ✅ Read / ✅ Write | `.bz2 .tbz2` | ✅ Read / ✅ Write |
| xz | ✅ Read / ✅ Write | `.xz .txz` | — |
| wim | ✅ Read / ✅ Write | `.wim .swm .esd` | — |
| PPMd | ✅ Read / ❌ **Write** | `.pmd` | ✅ Read / ✅ Write (`-m0=PPMd`) |
| LZMA | ✅ Read / ❌ **Write** | `.lzma` | ✅ Read / ✅ Write (`-m0=LZMA`) |
| LZMA86 | ✅ Read / ❌ **Write** | `.lzma86` | — |
| zstd | ✅ Read / ❌ **Write** | `.zst .tzst` | — (not a 7z codec) |
| Rar | ✅ Read / ❌ **Write** | `.rar` | — |
| Rar5 | ✅ Read / ❌ **Write** | `.rar` | — |
| Cab | ✅ Read / ❌ **Write** | `.cab` | — |
| Iso | ✅ Read / ❌ **Write** | `.iso .img` | — |
| Nsis | ✅ Read / ❌ **Write** | `.nsis` | — |
| Dmg | ✅ Read / ❌ **Write** | `.dmg` | — |
| NTFS | ✅ Read / ❌ **Write** | `.ntfs .img` | — |
| Ext | ✅ Read / ❌ **Write** | `.ext .ext2 .ext3 .ext4 .img` | — |
| VHD | ✅ Read / ❌ **Write** | `.vhd` | — |
| VHDX | ✅ Read / ❌ **Write** | `.vhdx .avhdx` | — |
| VMDK | ✅ Read / ❌ **Write** | `.vmdk` | — |
| QCOW | ✅ Read / ❌ **Write** | `.qcow .qcow2` | — |
| GPT | ✅ Read / ❌ **Write** | `.gpt .mbr` | — |
| MBR | ✅ Read / ❌ **Write** | `.mbr` | — |
| FAT | ✅ Read / ❌ **Write** | `.fat .img` | — |
| HFS | ✅ Read / ❌ **Write** | `.hfs .hfsx` | — |
| APFS | ✅ Read / ❌ **Write** | `.apfs .img` | — |
| Udf | ✅ Read / ❌ **Write** | `.udf .iso .img` | — |
| SquashFS | ✅ Read / ❌ **Write** | `.squashfs` | — |
| CramFS | ✅ Read / ❌ **Write** | `.cramfs` | — |
| PE | ✅ Read / ❌ **Write** | `.exe .dll .sys` | — |
| ELF | ✅ Read / ❌ **Write** | `.elf` | — |
| MachO | ✅ Read / ❌ **Write** | `.macho` | — |
| Chm | ✅ Read / ❌ **Write** | `.chm .chi .chq` | — |
| Compound | ✅ Read / ❌ **Write** | `.msi .doc .xls .ppt` | — |
| Cpio | ✅ Read / ❌ **Write** | `.cpio` | — |
| Rpm | ✅ Read / ❌ **Write** | `.rpm` | — |
| Ar | ✅ Read / ❌ **Write** | `.ar .a .deb .lib` | — |
| Arj | ✅ Read / ❌ **Write** | `.arj` | — |
| Lzh | ✅ Read / ❌ **Write** | `.lzh .lha` | — |
| Z | ✅ Read / ❌ **Write** | `.z .taz` | — |
| Split | ✅ Read / ❌ **Write** | `.001` | — |
| Xar | ✅ Read / ❌ **Write** | `.xar .pkg .xip` | — |
| Hxs | ✅ Read / ❌ **Write** | `.hxs .hxi .lit` | — |
| FLV | ✅ Read / ❌ **Write** | `.flv` | — |
| SWF | ✅ Read / ❌ **Write** | `.swf` | — |
| SWFc | ✅ Read / ❌ **Write** | `.swf` (compressed) | — |
| Base64 | ✅ Read / ❌ **Write** | `.b64` | — |
| IHex | ✅ Read / ❌ **Write** | `.ihex` | — |
| COFF | ✅ Read / ❌ **Write** | `.obj` | — |
| TE | ✅ Read / ❌ **Write** | `.te` | — |

The **codecs** available inside `.7z` (and sometimes `.zip`) are:

| Codec | Encode | Decode | Zeven API | Notes |
|---|---|---|---|---|
| LZMA2 | ✅ | ✅ | `Lzma2Codec` / `Lzma2Stream` | Default for `.7z` |
| LZMA | ✅ | ✅ | — | Legacy default |
| PPMd | ✅ | ✅ | `PpmdCodec` / `PpmdStream` | Good for text; chunked format |
| BZip2 | ✅ | ✅ | — | |
| Deflate | ✅ | ✅ | — | Used by `.zip` |
| Deflate64 | ✅ | ✅ | — | |
| Copy | ✅ | ✅ | — | No compression (store) |
| Delta | ✅ | ✅ | — | Filter |
| BCJ / BCJ2 | ✅ | ✅ | — | x86 executable filter |
| ARM / ARM64 | ✅ | ✅ | — | ARM executable filter |
| RISCV | ✅ | ✅ | — | RISC-V executable filter |
| 7zAES | ✅ | ✅ | — | AES-256 encryption |
| Zstandard | ✅ | ✅ | `ZstdCodec` / `ZstdStream` | Via 7-Zip-zstd; chunked format |
| Brotli | ✅ | ✅ | `BrotliCodec` / `BrotliStream` | Via 7-Zip-zstd; chunked format |
| LZ4 | ✅ | ✅ | `Lz4Codec` / `Lz4Stream` | Via 7-Zip-zstd; chunked format |
| LZ5 | ✅ | ✅ | — | Via 7-Zip-zstd; deprecated, use LZ4 |
| Lizard | ✅ | ✅ | — | Via 7-Zip-zstd; deprecated, use Zstd |
| Fast LZMA2 | ✅ | ✅ | — | Via 7-Zip-zstd (same ID as LZMA2) |

The full list of read/write support is reported at runtime via `GetHandlerProperty2(kUpdate)` — see `ZevenLibrary.Formats`.

### Exported Functions

All DLLs export the same core COM factory function:

```
CreateObject(const GUID *clsID, const GUID *iid, void **outObject) → HRESULT
```

Additional exports for format/codec enumeration:

| Export | `7z.dll` | `7za.dll` | `7zxa.dll` |
|---|---|---|---|
| `CreateObject` | ✅ | ✅ | ✅ |
| `GetNumberOfFormats` | ✅ | ✅ | ✅ |
| `GetHandlerProperty2` | ✅ | ✅ | ✅ |
| `GetNumberOfMethods` | ✅ | ✅ | ❌ |
| `GetMethodProperty` | ✅ | ✅ | ❌ |
| `CreateDecoder` | ✅ | ✅ | ❌ |
| `CreateEncoder` | ✅ | ✅ | ❌ |
| `GetHashers` | ✅ | ✅ | ❌ |
| `SetCodecs` | ✅ | ✅ | ✅ |

### COM Interface Architecture

7-Zip uses standard COM vtable layout (IUnknown-based) but does **not** support standard COM activation (`DllGetClassObject` / `CoCreateInstance`). Instead, objects are created via the `CreateObject` export with format-specific CLSIDs:

```
CLSID pattern: {23170F69-40C1-278A-1000-000110xx0000}
                                              ^^ format ID
```

Key format IDs: `01`=Zip, `07`=7z, `0C`=xz, `EE`=Tar, `EF`=GZip.

Key interface IIDs (all under `{23170F69-40C1-278A-0000-00ggnnss0000}`):

| Interface | Group | Sub | Purpose |
|---|---|---|---|
| `IInArchive` | `06` | `60` | Open, list, extract archives |
| `IOutArchive` | `06` | `A0` | Create/update archives |
| `IArchiveExtractCallback` | `06` | `20` | Provide output streams during extraction |
| `IArchiveUpdateCallback` | `06` | `80` | Provide input data during creation |
| `ISetProperties` | `06` | `03` | Configure compression settings |
| `ISequentialInStream` | `03` | `01` | Sequential read |
| `IInStream` | `03` | `03` | Seekable read |
| `ISequentialOutStream` | `03` | `02` | Sequential write |
| `IOutStream` | `03` | `04` | Seekable write |
| `ICryptoGetTextPassword` | `05` | `10` | Password for reading |
| `ICryptoGetTextPassword2` | `05` | `11` | Password for writing |

## Zeven Stream Format

All codecs share a common architecture via `ZevenStream<TOptions>` (generic base) and `ZevenCodec` (shared batch logic). Typed wrappers provide a discoverable API:

| Batch API | Streaming API | Codec |
|-----------|--------------|-------|
| `Lzma2Codec` | `Lzma2Stream` | LZMA2 |
| `PpmdCodec` | `PpmdStream` | PPMd |
| `ZstdCodec` | `ZstdStream` | Zstandard |
| `BrotliCodec` | `BrotliStream` | Brotli |
| `Lz4Codec` | `Lz4Stream` | LZ4 |

> **Note:** `Zeven.Core.BrotliStream` shares its name with `System.IO.Compression.BrotliStream`. Use a namespace alias or fully qualified name if both are needed in the same file.

This format is **not** compatible with 7z.exe or the .7z archive format.

### Why chunking?

Most 7-Zip encoders (PPMd, Zstd, Brotli, LZ4) expose only a batch `Code()` API — a single blocking call that reads all input and writes all output. There is no incremental encoding interface. To provide a streaming `Write()` API, data is buffered into fixed-size chunks (default 16 MB) and each chunk is compressed independently.

Many codecs also lack end-of-stream markers — the decoder must be told the exact output size. The chunked format stores per-chunk sizes to provide this.

### Wire format

```
Stream header (16 bytes fixed + N property + 4 CRC):
  [4 bytes magic: "ZVN\x01"]               Zeven format v1
  [8 bytes codec ID, LE]                   7-Zip codec ID (e.g., 0x030401 = PPMd)
  [2 bytes property header length, LE]     Size of property data (varies by codec; stored
                                           so the format is self-describing)
  [2 bytes reserved, zero]                 Future use
  [N bytes property header]                Codec-specific properties
  [4 bytes CRC32, LE]                      IEEE CRC-32 of property header

Per chunk:
  [8 bytes uncompressed size, LE]           Original data size (must be > 0)
  [8 bytes compressed size, LE]             Compressed payload size
  [N bytes compressed data]                 The payload
  [4 bytes CRC32, LE]                       IEEE CRC-32 of (sizes + data)

End marker:
  [16 bytes zero]                           Signals end of stream
```

CRC algorithm: IEEE CRC-32 (polynomial `0x04C11DB7`, reflected, init `0xFFFFFFFF`, final xor `0xFFFFFFFF`), serialized as little-endian `uint32`. Each chunk CRC covers the concatenation of the two 8-byte size fields and the compressed data.

### Chunk size configuration

```csharp
// Default: 16 MB chunks (works for any codec)
var stream = new ZstdStream(output, CompressionMode.Compress);

// Custom chunk size (e.g., 1 MB)
var options = new ZstdOptions { ChunkSize = 1 * 1024 * 1024 };
var stream = new ZstdStream(output, CompressionMode.Compress, options);

// Batch codecs also chunk using options.ChunkSize
ZstdCodec.Compress(input, output);
```

### Interoperability

- Each codec's batch and streaming APIs produce identical wire formats — output from `ZstdCodec` can be read by `ZstdStream` and vice versa. Same for PPMd, Brotli, and LZ4.
- Streams from different codecs are **not** interchangeable — the codec ID in the header identifies which codec produced the stream.
- This format is **not** compatible with 7z.exe. Archives use `.7z` container metadata for sizes; this library uses standalone chunked framing.

### Architecture

All streaming classes inherit from `ZevenStream<TOptions>`, which handles chunk buffering, CRC integrity, and COM lifecycle. Typed wrappers (`PpmdStream`, `ZstdStream`, etc.) are thin sealed classes that forward constructors. Batch codecs delegate to `ZevenCodec`.

The chunk buffer is rented from `ArrayPool<byte>` to avoid LOH allocations. Codec options are init-only to prevent mutation after construction.

## Implementation Notes

### Key Pitfalls

1. **`IInStream.Seek` `newPosition` parameter must be `nint`**, not `out ulong`. 7-Zip's `InStream_SeekSet` passes NULL for this parameter — using `out` causes the CCW to return `E_POINTER`.

2. **`IOutStream` is required** for 7z format creation, not just `ISequentialOutStream`. The 7z handler needs `Seek` and `SetSize` on the output stream.

3. **CCW GC prevention**: managed objects passed as COM callbacks must be kept alive (rooted) for the duration of native calls. Otherwise the GC collects them and native code crashes with `AccessViolationException`.

4. **Don't `NativeLibrary.Free` the DLL** while COM finalizers may still reference vtable pointers.

## Building

```
dotnet build
dotnet test tests\Zeven.Tests
```

Requires .NET 10 SDK and a copy of `7z.dll` at the configured path.

Set `ZEVEN_7Z_DLL_PATH` to override the default DLL location for tests:

```
set ZEVEN_7Z_DLL_PATH=C:\Program Files\7-Zip\7z.dll
dotnet test tests\Zeven.Tests
```

## Development Notes

### Brotli single-threaded encoding

The standalone Brotli archive handler (`.br`) forces single-threaded encoding. This is intentional — brotli-mt produces non-standard multi-threaded framing that standard Brotli decoders (including browsers) cannot read. The 7-Zip-zstd encoder explicitly calls `SetNumberOfThreads(0)` to ensure `.br` output is universally compatible.

As a result, `BrotliArchiveCreateOptions` does not expose a `NumThreads` option. The underlying Brotli codec does support `ICompressSetCoderMt`, and threaded Brotli compression is available through the codec API (`BrotliCodec` / `BrotliStream`), but the output uses Zeven's chunked format rather than the `.br` archive format.

### LZMA2 zero-copy streaming compress

LZMA2 is the only 7-Zip encoder that implements `ISequentialOutStream`, which would allow true push-based incremental compression without chunking or buffering. The current implementation uses chunking for consistency with the other codecs, but a future `Lzma2Stream` variant could leverage this for lower memory usage and latency on the compress path.

## License

Zeven is licensed under the [MIT License](LICENSE).

**7-Zip notice:** Zeven dynamically loads `7z.dll` at runtime but does not include or distribute it. 7-Zip is licensed under [LGPL-2.1](https://www.7-zip.org/license.txt) with portions under BSD-3-Clause and the unRAR restriction. [7-Zip-zstd](https://github.com/mcmilk/7-Zip-zstd) is also LGPL-2.1. Users are responsible for complying with 7-Zip's license when distributing `7z.dll` alongside their applications.
