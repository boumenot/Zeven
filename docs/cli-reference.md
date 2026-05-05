# 7-Zip Command Line Reference

Reference for `7z.exe` / `7za.exe` (version 26.01). Both executables accept the same commands and switches. `7z.exe` loads `7z.dll` for all format support; `7za.exe` is a standalone binary with limited formats.

```
Usage: 7z <command> [<switches>...] <archive_name> [<file_names>...] [@listfile]
```

## Commands

| Command | Description |
|---|---|
| `a` | **Add** files to archive (create or append) |
| `b` | **Benchmark** — test CPU speed with compression/decompression |
| `d` | **Delete** files from archive |
| `e` | **Extract** files (flat — ignores directory structure) |
| `h` | **Hash** — calculate hash values for files |
| `i` | **Info** — show supported formats, codecs, and hashers |
| `l` | **List** contents of archive |
| `rn` | **Rename** files within an archive |
| `t` | **Test** integrity of archive |
| `u` | **Update** files in archive (add new, replace modified) |
| `x` | **eXtract** files with full directory paths |

### `a` — Add / Create

Create a new archive or add files to an existing one.

```bash
# Create a 7z archive
7z a archive.7z file1.txt file2.txt dir/

# Create a zip archive
7z a -tzip archive.zip *.cpp *.h

# Create with maximum compression
7z a -mx9 archive.7z project/

# Create with PPMd compression (good for text)
7z a -m0=PPMd archive.7z *.txt

# Create with LZMA2 and 8 threads
7z a -mmt8 -m0=LZMA2 archive.7z data/

# Create encrypted archive (AES-256, encrypt headers too)
7z a -p"MyPassword" -mhe=on archive.7z secrets/

# Create multi-volume archive (100MB per volume)
7z a -v100m archive.7z large_folder/

# Create self-extracting archive
7z a -sfx archive.exe files/

# Add files recursively from subdirectories
7z a -r archive.7z src/*.cs

# Create archive and delete source files after
7z a -sdel archive.7z temp_files/

# Create tar.gz (tar first, then gzip)
7z a -ttar archive.tar files/
7z a -tgzip archive.tar.gz archive.tar
```

### `x` — Extract with full paths

```bash
# Extract to current directory
7z x archive.7z

# Extract to a specific directory
7z x archive.7z -o"C:\output"

# Extract with password
7z x -p"MyPassword" encrypted.7z

# Extract specific files
7z x archive.7z "docs/*.pdf" "readme.txt"

# Extract and overwrite existing files
7z x -aoa archive.7z

# Extract and skip existing files
7z x -aos archive.7z

# Extract and auto-rename collisions
7z x -aou archive.7z

# Extract and verify CRC
7z x -scrcCRC32 archive.7z
```

### `e` — Extract flat (no directory structure)

```bash
# Extract all files into a single directory (no subdirs)
7z e archive.7z -o"C:\flat_output"

# Useful for grabbing specific files regardless of path
7z e archive.7z "*.dll" -o"C:\libs"
```

### `l` — List

```bash
# List archive contents
7z l archive.7z

# Technical listing (sizes, attributes, methods, CRC)
7z l -slt archive.7z

# List specific files
7z l archive.7z "*.txt"
```

### `t` — Test

```bash
# Test archive integrity
7z t archive.7z

# Test with password
7z t -p"MyPassword" encrypted.7z

# Test and show hash values
7z t -scrcSHA256 archive.7z
```

### `d` — Delete

```bash
# Delete a file from archive
7z d archive.7z "old_file.txt"

# Delete by wildcard
7z d archive.7z "*.bak" -r
```

### `u` — Update

```bash
# Update archive with newer files
7z u archive.7z modified_file.txt

# Update: add new, replace modified, keep others
7z u archive.7z dir/
```

### `rn` — Rename

```bash
# Rename a file inside the archive
7z rn archive.7z "old_name.txt" "new_name.txt"

# Rename multiple files
7z rn archive.7z "a.txt" "x.txt" "b.txt" "y.txt"
```

### `h` — Hash

```bash
# Calculate SHA-256 hash of files
7z h -scrcSHA256 *.iso

# Calculate CRC32
7z h file.bin

# Calculate multiple hashes
7z h -scrc* important.dat
```

### `b` — Benchmark

```bash
# Run benchmark with default settings
7z b

# Benchmark with 4 threads
7z b -mmt4

# Benchmark with specific iteration count
7z b 3
```

## Switches

### Archive type

| Switch | Description |
|---|---|
| `-t{Type}` | Set archive type: `7z`, `zip`, `tar`, `gzip`, `bzip2`, `xz`, `wim` |
| `-stx{Type}` | Exclude archive type during extraction |

```bash
7z a -tzip output.zip files/       # Force zip format
7z a -t7z output.7z files/         # Force 7z format
7z a -ttar output.tar files/       # Create tar archive
```

### Compression method (`-m`)

| Switch | Description |
|---|---|
| `-mx[N]` | Compression level: `0` (store), `1` (fastest), `3` (fast), `5` (normal), `7` (maximum), `9` (ultra) |
| `-m0={Method}` | Compression method: `LZMA`, `LZMA2`, `PPMd`, `BZip2`, `Deflate`, `Copy` |
| `-mmt[N]` | Number of CPU threads (`on` = all cores) |
| `-ms={on\|off\|[e]Size}` | Solid block size (`on` = solid archive, `off` = non-solid) |
| `-mhe={on\|off}` | Encrypt archive headers (7z only — hides filenames) |
| `-md={Size}` | Dictionary size (e.g., `64m`, `256m`, `1g`) |
| `-mfb={N}` | Number of fast bytes for LZMA/Deflate |

```bash
# Store without compression
7z a -mx0 archive.7z files/

# Fastest compression
7z a -mx1 archive.7z files/

# Ultra compression with large dictionary
7z a -mx9 -md=256m archive.7z files/

# LZMA2 with 8 threads
7z a -m0=LZMA2 -mmt8 archive.7z files/

# PPMd for text files
7z a -m0=PPMd -mx9 archive.7z *.txt *.log

# Solid archive (better compression for many similar files)
7z a -ms=on archive.7z project/

# Encrypt headers (hides filenames)
7z a -p"secret" -mhe=on archive.7z private/

# Zip with Deflate64
7z a -tzip -mm=Deflate64 archive.zip files/
```

### Password and encryption

| Switch | Description |
|---|---|
| `-p{Password}` | Set password for encryption/decryption |
| `-mhe=on` | Encrypt archive headers (7z only — hides file names) |

```bash
# Create encrypted archive
7z a -p"P@ssw0rd!" archive.7z files/

# Create encrypted with hidden filenames
7z a -p"P@ssw0rd!" -mhe=on archive.7z files/

# Extract encrypted archive
7z x -p"P@ssw0rd!" archive.7z
```

### Output and directory

| Switch | Description |
|---|---|
| `-o{Dir}` | Set output directory for extraction |
| `-w[{Dir}]` | Set working directory (temp files go here) |
| `-so` | Write data to stdout |
| `-si[{name}]` | Read data from stdin |

```bash
# Extract to specific directory
7z x archive.7z -o"C:\output"

# Pipe extraction to stdout
7z e archive.7z -so file.txt > output.txt

# Pipe stdin to archive
cat file.txt | 7z a -si"file.txt" archive.7z
```

### Overwrite mode (`-ao`)

| Switch | Behavior |
|---|---|
| `-aoa` | **Overwrite All** existing files without prompt |
| `-aos` | **Skip** extracting if file already exists |
| `-aot` | Rename existing file (append `_1`, `_2`, etc.) |
| `-aou` | Rename extracted file to avoid collision |

### Include/Exclude files

| Switch | Description |
|---|---|
| `-i{spec}` | Include filenames matching pattern |
| `-x{spec}` | Exclude filenames matching pattern |
| `-ai{spec}` | Include archive names (for multi-archive operations) |
| `-ax{spec}` | Exclude archive names |
| `-r[-\|0]` | Recurse subdirectories: `-r` (all), `-r0` (wildcard names only), `-r-` (disable) |
| `-spd` | Disable wildcard matching for file names |

Specifier format: `[r[-|0]][m[-|2]][w[-]]{@listfile|!wildcard}`

```bash
# Exclude *.bak files
7z a archive.7z dir/ -x!*.bak

# Exclude multiple patterns
7z a archive.7z src/ -x!*.obj -x!*.pdb -x!bin/

# Include only *.cs files recursively
7z a archive.7z -ir!*.cs src/

# Use a list file
7z a archive.7z @filelist.txt

# Exclude from list file
7z a archive.7z dir/ -x@excludes.txt
```

### Logging and progress

| Switch | Description |
|---|---|
| `-bb[0-3]` | Log level: `0` (disable), `1` (names), `2` (names+operations), `3` (verbose) |
| `-bd` | Disable progress indicator |
| `-bs{o\|e\|p}{0\|1\|2}` | Redirect output/error/progress: `0` (disable), `1` (stdout), `2` (stderr) |
| `-bt` | Show execution time statistics |
| `-slt` | Show technical info in list command |

```bash
# Quiet mode (no output except errors)
7z a -bb0 -bd archive.7z files/

# Verbose mode
7z a -bb3 archive.7z files/

# Redirect progress to stderr
7z a -bsp2 archive.7z files/

# Technical listing
7z l -slt archive.7z
```

### Filesystem and links

| Switch | Description |
|---|---|
| `-snh` | Store hard links as links |
| `-snl` | Store symbolic links as links |
| `-sni` | Store NT security information |
| `-sns[-]` | Store NTFS alternate streams |
| `-ssp` | Don't change Last Access Time of source files |
| `-ssw` | Compress files open for writing (shared files) |
| `-stl` | Set archive timestamp from most recently modified file |
| `-spf[2]` | Use fully qualified file paths |

```bash
# Preserve symbolic and hard links
7z a -snl -snh archive.7z dir/

# Preserve NT security and alternate streams
7z a -sni -sns archive.7z dir/

# Don't modify source file timestamps
7z a -ssp archive.7z dir/
```

### Hash functions (`-scrc`)

| Switch | Description |
|---|---|
| `-scrcCRC32` | CRC-32 (default for `t` command) |
| `-scrcCRC64` | CRC-64 |
| `-scrcSHA256` | SHA-256 |
| `-scrcSHA1` | SHA-1 |
| `-scrcXXH64` | XXHash-64 (fast) |
| `-scrc*` | All available hash functions |

```bash
7z h -scrcSHA256 *.iso          # SHA-256 hash of files
7z t -scrcCRC32 archive.7z      # Test with CRC-32 verification
7z h -scrc* important.dat       # All hashes
```

### Character encoding

| Switch | Description |
|---|---|
| `-scc{encoding}` | Console input/output charset: `UTF-8`, `WIN`, `DOS` |
| `-scs{encoding}` | List file charset: `UTF-8`, `UTF-16LE`, `UTF-16BE`, `WIN`, `DOS` |

### Multi-volume

| Switch | Description |
|---|---|
| `-v{Size}[b\|k\|m\|g]` | Create volumes of specified size |

```bash
# Split into 100MB volumes
7z a -v100m archive.7z large_file.iso
# Creates: archive.7z.001, archive.7z.002, ...

# Split into 700MB (CD-sized)
7z a -v700m archive.7z data/

# Split into 4480MB (DVD-sized)
7z a -v4480m archive.7z data/
```

### Miscellaneous

| Switch | Description |
|---|---|
| `-y` | Assume Yes on all prompts |
| `-an` | Disable archive name field (use with `-ai` for multi-archive ops) |
| `-slp` | Set large pages mode (requires admin) |
| `-stm{HexMask}` | Set CPU thread affinity mask |
| `-ssc[-]` | Case-sensitive mode (`-ssc` = on, `-ssc-` = off) |
| `-sse` | Stop if any input file can't be opened |
| `-spe` | Eliminate duplication of root folder during extraction |
| `-sa{a\|e\|s}` | Archive name mode: `a` (add extension), `e` (exact), `s` (always add) |
| `-sfx[{name}]` | Create self-extracting archive |
| `-sdel` | Delete source files after successful compression |
| `--` | Stop switch parsing (treat remaining args as filenames) |

## Common Recipes

### Backup with encryption and maximum compression

```bash
7z a -t7z -mx9 -mhe=on -p"BackupPass!" -r backup.7z "C:\Projects" -x!*.obj -x!bin/ -x!node_modules/
```

### Create a tar.gz (Linux-compatible)

```bash
7z a -ttar archive.tar files/
7z a -tgzip archive.tar.gz archive.tar
del archive.tar
```

### Extract and verify integrity

```bash
7z x -scrcSHA256 archive.7z -o"C:\output" && echo "OK"
```

### Batch compress files individually

```bash
for %f in (*.log) do 7z a -tgzip "%f.gz" "%f" -sdel
```

### Test all archives in a directory

```bash
for %f in (*.7z) do 7z t "%f"
```

### Diff: list files in two archives

```bash
7z l archive_old.7z > old.txt
7z l archive_new.7z > new.txt
fc old.txt new.txt
```

### Stream to/from pipe

```bash
# Compress stdin
echo "hello" | 7z a -si -tgzip archive.gz

# Extract to stdout
7z e archive.gz -so > output.txt
```

### Update only newer files

```bash
7z u archive.7z -uq0 updated_files/
```

## Exit Codes

| Code | Meaning |
|---|---|
| `0` | No error |
| `1` | Warning (non-fatal, e.g., locked files skipped) |
| `2` | Fatal error |
| `7` | Command line error |
| `8` | Not enough memory |
| `255` | User stopped the process |
