# Code Style Guidelines

## Naming

| Element | Convention | Example |
|---|---|---|
| Field (instance or static) | camelCase, **no** leading `_` | `bool disposed;` |
| Class, Struct, Record | PascalCase | `ZevenLibrary` |
| Interface | PascalCase with `I` prefix | `IInArchive` |
| Property | PascalCase | `Formats` |
| Method, Function | PascalCase | `CreateInArchive()` |
| Namespace | PascalCase | `Zeven.Core.Interop` |
| Local variable | camelCase | `archiveBytes` |
| Parameter | camelCase | `dllPath` |
| Constant | PascalCase | `VtEmpty` |

## Naming — Avoid "Helper"

Do not use `Helper`, `Utils`, `Manager`, or similar suffixes. Name classes after what they *do*, not that they *help*. For example: `Codec` not `CodecHelper`, `Archive` not `ArchiveHelper`.

## Braces

`if`, `else`, `for`, `foreach`, `while`, `do`, `using`, and `lock` statements always use braces, even for single-line bodies.

```csharp
// ✅ correct
if (size == 0)
{
    return 0;
}

// ❌ wrong
if (size == 0)
    return 0;
```

## Parameter Line Breaks

When a method signature doesn't fit on one line, break after the opening parenthesis. Continuation parameters are indented by 8 spaces (two levels).

```csharp
// ✅ correct
public static void Compress(ICodecOptions options, Stream input, Stream output,
        bool writeSizePrefix = true)

public static nint InitStreamDecoder(ulong codecId,
        Stream input, StrategyBasedComWrappers cw, List<object> liveObjects,
        bool hasSizePrefix = true)

// ❌ wrong — aligned to opening paren
public static void Compress(ICodecOptions options,
                            Stream input,
                            Stream output)
```

## Instance Members

Instance fields, properties, and methods must be prefixed with `this.` to make scope explicit.

```csharp
// ✅ correct
public void Open(Stream stream)
{
    this.streamWrapper = new InStreamWrapper(stream);
    this.Archive.Close();
}

// ❌ wrong
public void Open(Stream stream)
{
    streamWrapper = new InStreamWrapper(stream);
    Archive.Close();
}
```

This applies inside instance methods and constructors. It does **not** apply to:
- Static members (use the class name or no prefix)
- Local variables and parameters
- Base class calls (`base.Method()`)

## File Naming

When a file contains a single type, the filename must match the type name (e.g., `ZevenStream.cs` for `ZevenStream<TOptions>`, `IArchiveCreateOptions.cs` for `IArchiveCreateOptions`).
