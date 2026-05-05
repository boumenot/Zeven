using System.Runtime.InteropServices;

namespace Zeven.Interop;

/// <summary>
/// Blittable PROPVARIANT layout (24 bytes on x64).
/// The union portion starts at offset 8 and can hold scalars or a pointer.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 24)]
public struct PropVariant
{
    [FieldOffset(0)] public ushort VarType;
    [FieldOffset(2)] public ushort Reserved1;
    [FieldOffset(4)] public ushort Reserved2;
    [FieldOffset(6)] public ushort Reserved3;

    // Union — all at offset 8
    [FieldOffset(8)] public int    IntValue;
    [FieldOffset(8)] public uint   UIntValue;
    [FieldOffset(8)] public long   LongValue;
    [FieldOffset(8)] public ulong  ULongValue;
    [FieldOffset(8)] public nint   PointerValue;
    [FieldOffset(8)] public short  BoolValue;   // VARIANT_BOOL (-1 = true, 0 = false)

    // VT constants
    public const ushort VT_EMPTY    = 0;
    public const ushort VT_I2       = 2;
    public const ushort VT_I4       = 3;
    public const ushort VT_BSTR     = 8;
    public const ushort VT_BOOL     = 11;
    public const ushort VT_UI1      = 17;
    public const ushort VT_UI2      = 18;
    public const ushort VT_UI4      = 19;
    public const ushort VT_I8       = 20;
    public const ushort VT_UI8      = 21;
    public const ushort VT_FILETIME = 64;

    /// <summary>Read a VT_BSTR value as a managed string. Returns null if not VT_BSTR.</summary>
    public readonly string? GetBstr()
    {
        if (VarType != VT_BSTR || PointerValue == nint.Zero) return null;
        return Marshal.PtrToStringBSTR(PointerValue);
    }

    /// <summary>Read a VT_BOOL value. VARIANT_BOOL: -1 = true, 0 = false.</summary>
    public readonly bool GetBool() => VarType == VT_BOOL && BoolValue != 0;

    /// <summary>Read a VT_UI8 or VT_I8 value as ulong.</summary>
    public readonly ulong GetUInt64() => VarType is VT_UI8 or VT_I8 ? ULongValue : 0;

    /// <summary>Read a VT_UI4 or VT_I4 value as uint.</summary>
    public readonly uint GetUInt32() => VarType is VT_UI4 or VT_I4 ? UIntValue : 0;

    /// <summary>Read a VT_FILETIME value as a nullable DateTime.</summary>
    public readonly DateTime? GetFileTime()
    {
        if (VarType != VT_FILETIME || LongValue == 0) return null;
        return DateTime.FromFileTime(LongValue);
    }
}

/// <summary>P/Invoke helpers for PROPVARIANT lifetime management.</summary>
public static partial class NativeMethods
{
    [LibraryImport("ole32.dll")]
    public static partial int PropVariantClear(ref PropVariant pvar);
}

/// <summary>7-Zip property IDs from PropID.h.</summary>
public static class PropId
{
    public const uint kpidNoProperty = 0;
    public const uint kpidPath       = 3;
    public const uint kpidName       = 4;
    public const uint kpidIsDir      = 6;
    public const uint kpidSize       = 7;
    public const uint kpidPackSize   = 8;
    public const uint kpidAttrib     = 9;
    public const uint kpidCTime      = 10;
    public const uint kpidATime      = 11;
    public const uint kpidMTime      = 12;
    public const uint kpidEncrypted  = 14;
    public const uint kpidCRC        = 19;
    public const uint kpidMethod     = 22;
}

/// <summary>Handler property IDs (NArchive::NHandlerPropID).</summary>
public static class HandlerPropId
{
    public const uint kName       = 0;
    public const uint kClassID    = 1;
    public const uint kExtension  = 2;
    public const uint kUpdate     = 4;
}
