# Zeven

A .NET 10 registration-free COM interop wrapper for 7-Zip's native DLLs, using source-generated COM interfaces (`[GeneratedComInterface]` / `[GeneratedComClass]`).

No COM registration, no IDL, no type libraries — just P/Invoke `CreateObject` + source-generated vtable proxies.

## Quick Start

```csharp
using Zeven;

using var lib = ZevenLibrary.Load(@"path\to\7z.dll");
var fmt = lib.Formats.First(f => f.Name == "7z");

// Create an archive in memory
var files = new Dictionary<string, byte[]>
{
    ["hello.txt"] = "Hello, World!"u8.ToArray(),
};
using var outStream = new MemoryStream();
lib.CreateArchive(fmt.ClassId, outStream, files);

// Read it back
using var handle = lib.CreateInArchive(fmt.ClassId);
handle.Open(new MemoryStream(outStream.ToArray()));
var extracted = handle.ExtractAll(); // Dict<string, byte[]>
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
| LZ5 | ✅ | ✅ | — | Via 7-Zip-zstd (`0x4F71105`) |
| Lizard | ✅ | ✅ | — | Via 7-Zip-zstd (`0x4F71106`) |
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

The following codecs have batch and streaming APIs that use the Zeven chunked wire format:

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
  [4 bytes codec ID, LE]                   7-Zip codec ID (e.g., 0x030401 = PPMd)
  [2 bytes property header length, LE]     Size of property data (varies by codec; stored
                                           so the format is self-describing)
  [6 bytes reserved, zero]                 Future use
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

// Batch codecs always write a single chunk (entire input)
ZstdCodec.Compress(input, output);
```

### Interoperability

- Each codec's batch and streaming APIs produce identical wire formats — output from `ZstdCodec` can be read by `ZstdStream` and vice versa. Same for PPMd, Brotli, and LZ4.
- Streams from different codecs are **not** interchangeable — the codec ID in the header identifies which codec produced the stream.
- This format is **not** compatible with 7z.exe. Archives use `.7z` container metadata for sizes; this library uses standalone chunked framing.

## Implementation Notes

### Key Pitfalls

1. **`IInStream.Seek` `newPosition` parameter must be `nint`**, not `out ulong`. 7-Zip's `InStream_SeekSet` passes NULL for this parameter — using `out` causes the CCW to return `E_POINTER`.

2. **`IOutStream` is required** for 7z format creation, not just `ISequentialOutStream`. The 7z handler needs `Seek` and `SetSize` on the output stream.

3. **CCW GC prevention**: managed objects passed as COM callbacks must be kept alive (rooted) for the duration of native calls. Otherwise the GC collects them and native code crashes with `AccessViolationException`.

4. **Don't `NativeLibrary.Free` the DLL** while COM finalizers may still reference vtable pointers.

## Building

```
dotnet build
dotnet test Tests
dotnet run
```

Requires .NET 10 SDK and a copy of `7z.dll` (from 7-Zip installer) at the configured path.
