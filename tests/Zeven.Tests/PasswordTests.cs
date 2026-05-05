using Zeven.Core;

namespace Zeven.Tests;

public class PasswordTests
{
    const string DllPath = @"q:\7z2601-bin\x64\7z.dll";

    [Fact]
    public void CreateEncrypted_RoundTrip_WithCorrectPassword()
    {
        var files = new Dictionary<string, byte[]>
        {
            ["secret.txt"] = "Top secret content!"u8.ToArray(),
        };
        const string password = "MyP@ssw0rd!";

        using var lib = new ZevenLibrary(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");

        // Create encrypted archive
        byte[] archiveBytes;
        using (var outStream = new MemoryStream())
        {
            lib.CreateArchive(format.ClassId, outStream, files, password: password);
            archiveBytes = outStream.ToArray();
        }

        // Read back with correct password
        using var handle = lib.CreateInArchive(format.ClassId);
        using var inStream = new MemoryStream(archiveBytes);
        handle.Open(inStream, password: password);

        var extracted = handle.ExtractAll();
        Assert.Equal(files["secret.txt"], extracted.Values.First());
    }

    [Fact]
    public void CreateEncrypted_WrongPassword_ProducesNoData()
    {
        var files = new Dictionary<string, byte[]>
        {
            ["secret.txt"] = "Top secret!"u8.ToArray(),
        };

        using var lib = new ZevenLibrary(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");

        byte[] archiveBytes;
        using (var outStream = new MemoryStream())
        {
            lib.CreateArchive(format.ClassId, outStream, files, password: "correct");
            archiveBytes = outStream.ToArray();
        }

        using var handle = lib.CreateInArchive(format.ClassId);
        using var inStream = new MemoryStream(archiveBytes);
        handle.Open(inStream, password: "wrong");

        // With wrong password, extraction produces no data (items have errors)
        var extracted = handle.ExtractAll();
        Assert.Empty(extracted);
    }
}
