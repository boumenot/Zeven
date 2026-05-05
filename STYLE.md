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
