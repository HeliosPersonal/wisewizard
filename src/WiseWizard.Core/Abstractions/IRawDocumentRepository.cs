using WiseWizard.Core.Models;

namespace WiseWizard.Core.Abstractions;

/// <summary>Persistence for collected Raw documents.</summary>
public interface IRawDocumentRepository
{
    /// <summary>
    /// Stores a document if no document with the same content hash already exists for the Run.
    /// Returns true if stored, false if it was a duplicate that was skipped.
    /// </summary>
    Task<bool> AddIfNewAsync(RawDocument document, CancellationToken ct = default);

    /// <summary>Reads all Raw documents collected for a Run, optionally filtered by Ticker.</summary>
    Task<IReadOnlyList<RawDocument>> GetForRunAsync(long runId, Ticker? ticker = null, CancellationToken ct = default);

    /// <summary>Deletes documents fetched before the given cutoff (retention cleanup). Returns rows removed.</summary>
    Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default);
}
