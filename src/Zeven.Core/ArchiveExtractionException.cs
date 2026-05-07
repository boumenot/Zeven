namespace Zeven.Core;

/// <summary>
/// 7-Zip extraction operation result codes from NArchive::NExtract::NOperationResult.
/// </summary>
public enum ExtractionResult
{
    OK = 0,
    UnsupportedMethod = 1,
    DataError = 2,
    CrcError = 3,
    Unavailable = 4,
    UnexpectedEnd = 5,
    DataAfterEnd = 6,
    IsNotArc = 7,
    HeadersError = 8,
    WrongPassword = 9,
}

/// <summary>
/// Describes a single entry that failed during extraction.
/// </summary>
public record ExtractionFailure(uint Index, ExtractionResult Result);

/// <summary>
/// Thrown when one or more archive entries fail during extraction.
/// </summary>
public class ArchiveExtractionException : Exception
{
    public IReadOnlyList<ExtractionFailure> Failures { get; }

    public ArchiveExtractionException(IReadOnlyList<ExtractionFailure> failures)
        : base(FormatMessage(failures))
    {
        this.Failures = failures;
    }

    private static string FormatMessage(IReadOnlyList<ExtractionFailure> failures)
    {
        if (failures.Count == 1)
        {
            return $"Extraction failed for entry {failures[0].Index}: {failures[0].Result}.";
        }
        return $"Extraction failed for {failures.Count} entries.";
    }
}
