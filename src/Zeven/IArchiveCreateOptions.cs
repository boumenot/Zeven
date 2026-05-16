namespace Zeven;

/// <summary>
/// Options for archive creation. Implementations return property name/value
/// pairs that are passed to 7-Zip's ISetProperties interface.
/// </summary>
public interface IArchiveCreateOptions
{
    /// <summary>
    /// Returns the archive properties as name/value pairs.
    /// Names follow 7-Zip's property naming (e.g., "x" for level, "0" for method).
    /// Values can be uint, string, bool, or ulong.
    /// </summary>
    IEnumerable<(string Name, object Value)> GetProperties();
}
