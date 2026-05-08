using Zeven.Core;

namespace Zeven.Tests;

public class PasswordTests
{
    static string DllPath => TestPaths.DllPath;

    [Fact]
    public void CreateEncrypted_RoundTrip_WithCorrectPassword()
    {
        var files = new Dictionary<string, byte[]>
        {
            ["secret.txt"] = "Top secret content!"u8.ToArray(),
        };
        const string password = "MyP@ssw0rd!";

        using var lib = ZevenLibrary.Load(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");

        // Create encrypted archive
        byte[] archiveBytes;
        using (var outStream = new MemoryStream())
        {
            lib.CreateArchive(format.ClassId, outStream, files, password: password);
            archiveBytes = outStream.ToArray();
        }

        // Read back with correct password
        using var inStream = new MemoryStream(archiveBytes);
        using var handle = lib.OpenArchive(format.ClassId, inStream, password: password);

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

        using var lib = ZevenLibrary.Load(DllPath);
        var format = lib.Formats.First(f => f.Name == "7z");

        byte[] archiveBytes;
        using (var outStream = new MemoryStream())
        {
            lib.CreateArchive(format.ClassId, outStream, files, password: "correct");
            archiveBytes = outStream.ToArray();
        }

        using var inStream = new MemoryStream(archiveBytes);
        using var handle = lib.OpenArchive(format.ClassId, inStream, password: "wrong");

        // With wrong password, extraction now throws ArchiveExtractionException
        var ex = Assert.Throws<ArchiveExtractionException>(() => handle.ExtractAll());
        Assert.All(ex.Failures, f => Assert.NotEqual(ExtractionResult.OK, f.Result));
    }
}
