using System.Runtime.InteropServices;
using Zeven.Interop;

namespace Zeven.Tests;

public class InStreamWrapperTests
{
    [Fact]
    public void Read_ReturnsData()
    {
        // Arrange
        byte[] sourceData = new byte[] { 1, 2, 3, 4, 5 };
        var stream = new MemoryStream(sourceData);
        var wrapper = new InStreamWrapper(stream);
        byte[] buffer = new byte[5];

        // Act
        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                int hr = wrapper.Read((nint)ptr, (uint)buffer.Length, out uint processed);

                // Assert
                Assert.Equal(0, hr); // S_OK
                Assert.Equal(5u, processed);
                Assert.Equal(sourceData, buffer);
            }
        }
    }

    [Fact]
    public void Read_ZeroSize_ReturnsZero()
    {
        // Arrange
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var wrapper = new InStreamWrapper(stream);

        // Act
        unsafe
        {
            byte[] buffer = new byte[10];
            fixed (byte* ptr = buffer)
            {
                int hr = wrapper.Read((nint)ptr, 0, out uint processed);

                // Assert
                Assert.Equal(0, hr); // S_OK
                Assert.Equal(0u, processed);
            }
        }
    }

    [Fact]
    public void Read_AtEnd_ReturnsZeroProcessed()
    {
        // Arrange
        var stream = new MemoryStream();
        var wrapper = new InStreamWrapper(stream);
        byte[] buffer = new byte[10];

        // Act
        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                int hr = wrapper.Read((nint)ptr, (uint)buffer.Length, out uint processed);

                // Assert
                Assert.Equal(0, hr); // S_OK
                Assert.Equal(0u, processed);
            }
        }
    }

    [Fact]
    public void Seek_SetsPosition()
    {
        // Arrange
        byte[] data = new byte[] { 0, 1, 2, 3, 4, 5 };
        var stream = new MemoryStream(data);
        var wrapper = new InStreamWrapper(stream);

        // Act
        int hr = wrapper.Seek(3, (uint)SeekOrigin.Begin, nint.Zero);

        // Assert
        Assert.Equal(0, hr); // S_OK
        Assert.Equal(3, stream.Position);
    }

    [Fact]
    public void Seek_ReturnsNewPosition()
    {
        // Arrange
        byte[] data = new byte[] { 0, 1, 2, 3, 4, 5 };
        var stream = new MemoryStream(data);
        var wrapper = new InStreamWrapper(stream);

        // Act
        unsafe
        {
            ulong newPos = 0;
            int hr = wrapper.Seek(5, (uint)SeekOrigin.Begin, (nint)(&newPos));

            // Assert
            Assert.Equal(0, hr); // S_OK
            Assert.Equal(5UL, newPos);
        }
    }
}

public class OutStreamWrapperTests
{
    [Fact]
    public void Write_WritesData()
    {
        // Arrange
        var stream = new MemoryStream();
        var wrapper = new OutStreamWrapper(stream);
        byte[] dataToWrite = new byte[] { 10, 20, 30, 40, 50 };

        // Act
        unsafe
        {
            fixed (byte* ptr = dataToWrite)
            {
                int hr = wrapper.Write((nint)ptr, (uint)dataToWrite.Length, out uint processed);

                // Assert
                Assert.Equal(0, hr); // S_OK
                Assert.Equal(5u, processed);
                Assert.Equal(dataToWrite, stream.ToArray());
            }
        }
    }

    [Fact]
    public void Write_ZeroSize_WritesNothing()
    {
        // Arrange
        var stream = new MemoryStream();
        var wrapper = new OutStreamWrapper(stream);
        byte[] dataToWrite = new byte[] { 10, 20, 30 };

        // Act
        unsafe
        {
            fixed (byte* ptr = dataToWrite)
            {
                int hr = wrapper.Write((nint)ptr, 0, out uint processed);

                // Assert
                Assert.Equal(0, hr); // S_OK
                Assert.Equal(0u, processed);
                Assert.Empty(stream.ToArray());
            }
        }
    }

    [Fact]
    public void Write_ProcessedSize_MatchesInput()
    {
        // Arrange
        var stream = new MemoryStream();
        var wrapper = new OutStreamWrapper(stream);
        byte[] dataToWrite = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        // Act
        unsafe
        {
            fixed (byte* ptr = dataToWrite)
            {
                int hr = wrapper.Write((nint)ptr, (uint)dataToWrite.Length, out uint processed);

                // Assert
                Assert.Equal(0, hr); // S_OK
                Assert.Equal((uint)dataToWrite.Length, processed);
            }
        }
    }

    [Fact]
    public void Seek_SetsPosition()
    {
        // Arrange
        var stream = new MemoryStream();
        var wrapper = new OutStreamWrapper(stream);
        byte[] dataToWrite = new byte[] { 1, 2, 3, 4, 5 };

        // Write some data first
        unsafe
        {
            fixed (byte* ptr = dataToWrite)
            {
                wrapper.Write((nint)ptr, (uint)dataToWrite.Length, out _);
            }
        }

        // Act
        int hr = wrapper.Seek(2, (uint)SeekOrigin.Begin, nint.Zero);

        // Assert
        Assert.Equal(0, hr); // S_OK
        Assert.Equal(2, stream.Position);
    }

    [Fact]
    public void Seek_ReturnsNewPosition()
    {
        // Arrange
        var stream = new MemoryStream();
        var wrapper = new OutStreamWrapper(stream);

        // Act
        unsafe
        {
            ulong newPos = 0;
            int hr = wrapper.Seek(10, (uint)SeekOrigin.Begin, (nint)(&newPos));

            // Assert
            Assert.Equal(0, hr); // S_OK
            Assert.Equal(10UL, newPos);
        }
    }

    [Fact]
    public void SetSize_SetsStreamLength()
    {
        // Arrange
        var stream = new MemoryStream();
        var wrapper = new OutStreamWrapper(stream);

        // Act
        int hr = wrapper.SetSize(100);

        // Assert
        Assert.Equal(0, hr); // S_OK
        Assert.Equal(100L, stream.Length);
    }

    [Fact]
    public void SetSize_Truncates()
    {
        // Arrange
        var stream = new MemoryStream();
        var wrapper = new OutStreamWrapper(stream);
        byte[] dataToWrite = new byte[100];
        Array.Fill(dataToWrite, (byte)42);

        // Write 100 bytes
        unsafe
        {
            fixed (byte* ptr = dataToWrite)
            {
                wrapper.Write((nint)ptr, (uint)dataToWrite.Length, out _);
            }
        }

        // Act
        int hr = wrapper.SetSize(50);

        // Assert
        Assert.Equal(0, hr); // S_OK
        Assert.Equal(50L, stream.Length);
    }
}
