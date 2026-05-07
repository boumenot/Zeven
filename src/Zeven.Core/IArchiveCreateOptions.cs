using System.Runtime.InteropServices.Marshalling;

namespace Zeven.Core;

/// <summary>
/// Interface for archive creation options. Each format implements this
/// with its own typed properties and marshals them via Apply().
/// </summary>
public interface IArchiveCreateOptions
{
    /// <summary>Apply these options to the archive handler via ISetProperties.</summary>
    void Apply(nint archivePtr, StrategyBasedComWrappers cw);
}
