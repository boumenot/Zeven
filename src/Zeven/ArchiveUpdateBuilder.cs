namespace Zeven;

public class ArchiveUpdateBuilder
{
    internal List<(string Path, UpdateAction Action, object? Source, long? Size)> operations = new();

    public ArchiveUpdateBuilder Add(string path, byte[] data)
    {
        this.operations.Add((path, UpdateAction.Add, data, data.Length));
        return this;
    }

    public ArchiveUpdateBuilder Add(string path, string filePath)
    {
        this.operations.Add((path, UpdateAction.Add, filePath, new FileInfo(filePath).Length));
        return this;
    }

    public ArchiveUpdateBuilder Add(string path, Stream data, long size)
    {
        this.operations.Add((path, UpdateAction.Add, data, size));
        return this;
    }

    public ArchiveUpdateBuilder Replace(string path, byte[] data)
    {
        this.operations.Add((path, UpdateAction.Replace, data, data.Length));
        return this;
    }

    public ArchiveUpdateBuilder Replace(string path, string filePath)
    {
        this.operations.Add((path, UpdateAction.Replace, filePath, new FileInfo(filePath).Length));
        return this;
    }

    public ArchiveUpdateBuilder Replace(string path, Stream data, long size)
    {
        this.operations.Add((path, UpdateAction.Replace, data, size));
        return this;
    }

    public ArchiveUpdateBuilder Delete(string path)
    {
        this.operations.Add((path, UpdateAction.Delete, null, null));
        return this;
    }

    internal enum UpdateAction { Add, Replace, Delete }
}
