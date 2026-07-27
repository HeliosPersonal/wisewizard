using WiseWizard.Core.Models;

namespace WiseWizard.Core.Services;

/// <summary>
/// A collection gap: a (Run, Ticker, Source) attempt that failed because the Source was
/// unreachable or signalled its allowed access rate was exceeded (PRD §5 AC-02). Recorded and
/// returned so the Run continues without aborting; other Sources and Tickers are unaffected.
/// </summary>
public sealed record CollectionGap
{
    public required long RunId { get; init; }
    public required Ticker Ticker { get; init; }
    public required SourceKind Source { get; init; }

    /// <summary>Human-readable reason for the gap (e.g. the failing exception message).</summary>
    public required string Reason { get; init; }
}

/// <summary>The per-(Ticker, Source) outcome of a collection attempt within a Run.</summary>
public sealed record SourceIngestResult
{
    public required Ticker Ticker { get; init; }
    public required SourceKind Source { get; init; }

    /// <summary>Documents fetched then kept after lookback filter and per-Source cap.</summary>
    public required int Fetched { get; init; }

    /// <summary>Documents newly stored (not duplicates within the Run).</summary>
    public required int Stored { get; init; }

    /// <summary>Documents skipped as duplicates within the Run (AC-04).</summary>
    public required int Skipped { get; init; }
}

/// <summary>
/// The summary returned by an ingest step for a Run: per-(Ticker, Source) counts plus the
/// collection gaps that were recorded. Zero fresh documents for a Ticker is a normal result,
/// not a failure (PRD §5 AC-07).
/// </summary>
public sealed record IngestionSummary
{
    public required long RunId { get; init; }
    public required IReadOnlyList<SourceIngestResult> Results { get; init; }
    public required IReadOnlyList<CollectionGap> Gaps { get; init; }

    /// <summary>Total documents newly stored across all Tickers and Sources.</summary>
    public int TotalStored => Results.Sum(r => r.Stored);

    /// <summary>Total documents skipped as duplicates across all Tickers and Sources.</summary>
    public int TotalSkipped => Results.Sum(r => r.Skipped);
}
