using WiseWizard.Core.Models;

namespace WiseWizard.Core.Abstractions;

/// <summary>Persistence for Runs, including resumable state.</summary>
public interface IRunRepository
{
    /// <summary>Creates a new Run row and returns it with its assigned id.</summary>
    Task<Run> CreateAsync(Run run, CancellationToken ct = default);

    /// <summary>Persists the full Run state (status, batch ids, cost/token accounting, failure reason).</summary>
    Task UpdateAsync(Run run, CancellationToken ct = default);

    /// <summary>Reads a Run by id, or null if not found.</summary>
    Task<Run?> GetAsync(long runId, CancellationToken ct = default);

    /// <summary>Reads the most recent Run with status <c>Finished</c>, or null if none.</summary>
    Task<Run?> GetLatestFinishedAsync(CancellationToken ct = default);

    /// <summary>Reads Runs that are neither finished nor failed (to resume after a restart).</summary>
    Task<IReadOnlyList<Run>> GetResumableAsync(CancellationToken ct = default);
}

/// <summary>Persistence for Extracted facts.</summary>
public interface IExtractedFactRepository
{
    Task AddRangeAsync(IReadOnlyList<ExtractedFact> facts, CancellationToken ct = default);

    /// <summary>Reads all facts for a Ticker within a Run (synthesis input + citations).</summary>
    Task<IReadOnlyList<ExtractedFact>> GetForRunTickerAsync(long runId, Ticker ticker, CancellationToken ct = default);
}

/// <summary>Persistence for Verdicts.</summary>
public interface IVerdictRepository
{
    /// <summary>Inserts or replaces a Verdict (idempotent per (RunId, Ticker) for resume safety).</summary>
    Task UpsertAsync(Verdict verdict, CancellationToken ct = default);

    /// <summary>Reads all Verdicts of a Run.</summary>
    Task<IReadOnlyList<Verdict>> GetForRunAsync(long runId, CancellationToken ct = default);

    /// <summary>Reads a single Verdict for a Ticker within a Run, or null.</summary>
    Task<Verdict?> GetAsync(long runId, Ticker ticker, CancellationToken ct = default);

    /// <summary>
    /// Reads the most recent Verdict for a Ticker from a Run strictly before the given one,
    /// used as the "what changed" delta baseline. Null when none exists.
    /// </summary>
    Task<Verdict?> GetPreviousAsync(Ticker ticker, long beforeRunId, CancellationToken ct = default);
}
