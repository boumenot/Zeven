using System.Runtime.InteropServices;
using Xunit;
using Zeven.Interop;

namespace Zeven.Tests;

public class PropVariantTests
{
    #region GetBool Tests

    [Fact]
    public void GetBool_VtBool_True()
    {
        var pv = new PropVariant { VarType = PropVariant.VT_BOOL, BoolValue = -1 };
        Assert.True(pv.GetBool());
    }

    [Fact]
    public void GetBool_VtBool_False()
    {
        var pv = new PropVariant { VarType = PropVariant.VT_BOOL, BoolValue = 0 };
        Assert.False(pv.GetBool());
    }

    [Fact]
    public void GetBool_WrongType_ReturnsFalse()
    {
        var pv = new PropVariant { VarType = PropVariant.VT_I4, BoolValue = -1 };
        Assert.False(pv.GetBool());
    }

    #endregion

    #region GetUInt64 Tests

    [Fact]
    public void GetUInt64_VtUI8_ReturnsValue()
    {
        var pv = new PropVariant { VarType = PropVariant.VT_UI8, ULongValue = 12345 };
        Assert.Equal(12345ul, pv.GetUInt64());
    }

    [Fact]
    public void GetUInt64_VtI8_ReturnsValue()
    {
        var pv = new PropVariant { VarType = PropVariant.VT_I8, ULongValue = 99 };
        Assert.Equal(99ul, pv.GetUInt64());
    }

    [Fact]
    public void GetUInt64_WrongType_ReturnsZero()
    {
        var pv = new PropVariant { VarType = PropVariant.VT_I4, ULongValue = 999 };
        Assert.Equal(0ul, pv.GetUInt64());
    }

    #endregion

    #region GetUInt32 Tests

    [Fact]
    public void GetUInt32_VtUI4_ReturnsValue()
    {
        var pv = new PropVariant { VarType = PropVariant.VT_UI4, UIntValue = 42 };
        Assert.Equal(42u, pv.GetUInt32());
    }

    [Fact]
    public void GetUInt32_VtI4_ReturnsValue()
    {
        var pv = new PropVariant { VarType = PropVariant.VT_I4, UIntValue = 7 };
        Assert.Equal(7u, pv.GetUInt32());
    }

    [Fact]
    public void GetUInt32_WrongType_ReturnsZero()
    {
        var pv = new PropVariant { VarType = PropVariant.VT_BOOL, UIntValue = 999 };
        Assert.Equal(0u, pv.GetUInt32());
    }

    #endregion

    #region GetFileTime Tests

    [Fact]
    public void GetFileTime_VtFileTime_ReturnsUtcDateTime()
    {
        var expected = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var fileTime = expected.ToFileTimeUtc();
        
        var pv = new PropVariant { VarType = PropVariant.VT_FILETIME, LongValue = fileTime };
        var result = pv.GetFileTime();
        
        Assert.NotNull(result);
        Assert.Equal(DateTimeKind.Utc, result.Value.Kind);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void GetFileTime_VtFileTime_ZeroValue_ReturnsNull()
    {
        var pv = new PropVariant { VarType = PropVariant.VT_FILETIME, LongValue = 0 };
        Assert.Null(pv.GetFileTime());
    }

    [Fact]
    public void GetFileTime_WrongType_ReturnsNull()
    {
        var pv = new PropVariant { VarType = PropVariant.VT_UI4, LongValue = 12345 };
        Assert.Null(pv.GetFileTime());
    }

    #endregion

    #region GetBstr Tests

    [Fact]
    public void GetBstr_WrongType_ReturnsNull()
    {
        var pv = new PropVariant { VarType = PropVariant.VT_I4, PointerValue = (nint)123 };
        Assert.Null(pv.GetBstr());
    }

    [Fact]
    public void GetBstr_NullPointer_ReturnsNull()
    {
        var pv = new PropVariant { VarType = PropVariant.VT_BSTR, PointerValue = nint.Zero };
        Assert.Null(pv.GetBstr());
    }

    [Fact]
    public void GetBstr_ValidBstr_ReturnsString()
    {
        const string testString = "hello";
        nint bstr = Marshal.StringToBSTR(testString);
        
        try
        {
            var pv = new PropVariant { VarType = PropVariant.VT_BSTR, PointerValue = bstr };
            var result = pv.GetBstr();
            
            Assert.NotNull(result);
            Assert.Equal(testString, result);
        }
        finally
        {
            Marshal.FreeBSTR(bstr);
        }
    }

    #endregion

    #region Default/Empty Tests

    [Fact]
    public void Default_VarType_IsEmpty()
    {
        var pv = default(PropVariant);
        Assert.Equal(PropVariant.VT_EMPTY, pv.VarType);
    }

    [Fact]
    public void Default_GetBool_ReturnsFalse()
    {
        var pv = default(PropVariant);
        Assert.False(pv.GetBool());
    }

    [Fact]
    public void Default_GetUInt64_ReturnsZero()
    {
        var pv = default(PropVariant);
        Assert.Equal(0ul, pv.GetUInt64());
    }

    [Fact]
    public void Default_GetUInt32_ReturnsZero()
    {
        var pv = default(PropVariant);
        Assert.Equal(0u, pv.GetUInt32());
    }

    [Fact]
    public void Default_GetFileTime_ReturnsNull()
    {
        var pv = default(PropVariant);
        Assert.Null(pv.GetFileTime());
    }

    [Fact]
    public void Default_GetBstr_ReturnsNull()
    {
        var pv = default(PropVariant);
        Assert.Null(pv.GetBstr());
    }

    #endregion
}
