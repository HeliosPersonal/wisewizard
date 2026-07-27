using Microsoft.Extensions.Logging;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;

namespace WiseWizard.Core.Services;

/// <summary>
/// The ingest step of the nightly pipeline (PRD §1; seq-ingest-ticker). For a Run and a caller-
/// supplied Universe of Tickers, collects Raw documents from every configured Source, applies the
/// lookback window and per-Source cap (AC-06), deduplicates within the Run (AC-04), records a
/// collection gap and continues when a Source fails (AC-02), and returns a summary. The CALLER
/// passes the Universe (Portfolio ∪ Watchlist); this service never scans market-wide (AC-05).
/// </summary>
public sealed class IngestionService(
    IEnumerable<IDataSource> sources,
    IRawDocumentRepository repository,
    IClock clock,
    ILogger<IngestionService> logger)
{
    /// <summary>Collection lookback window (PRD §6 — last 14 days of filings/news).</summary>
    public static readonly TimeSpan Lookback = TimeSpan.FromDays(14);

    /// <summary>Maximum documents kept per Source per Ticker per Run (PRD §6 AC-06).</summary>
    public const int MaxDocsPerSourcePerTicker = 15;

    private readonly IReadOnlyList<IDataSource> _sources =
        (sources ?? throw new ArgumentNullException(nameof(sources))).ToList();
    private readonly IRawDocumentRepository _repository = repository;
    private readonly IClock _clock = clock;
    private readonly ILogger<IngestionService> _logger = logger;

    /// <summary>
    /// Runs ingestion for the given Run over exactly the supplied Universe Tickers.
    /// </summary>
    /// <param name="runId">The Run collecting these documents.</param>
    /// <param name="universe">
    /// The Tickers to collect for — Portfolio ∪ Watchlist, supplied by the caller. Only these are
    /// ingested; a Ticker outside the Universe is never fetched (AC-05).
    /// </param>
    public async Task<IngestionSummary> IngestAsync(
        long runId, IEnumerable<Ticker> universe, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var since = now - Lookback;

        var results = new List<SourceIngestResult>();
        var gaps = new List<CollectionGap>();

        foreach (var ticker in universe)
        {
            foreach (var source in _sources)
            {
                ct.ThrowIfCancellationRequested();

                IReadOnlyList<RawDocument> fetched;
                try
                {
                    fetched = await source.FetchAsync(ticker, runId, since, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // AC-02: skip this Source, record the gap, continue with the others.
                    _logger.LogWarning(
                        ex,
                        "Collection gap for run {RunId} ticker {Ticker} source {Source}: {Reason}",
                        runId, ticker.Value, source.Kind, ex.Message);

                    gaps.Add(new CollectionGap
                    {
                        RunId = runId,
                        Ticker = ticker,
                        Source = source.Kind,
                        Reason = ex.Message,
                    });
                    continue;
                }

                // AC-06: keep only documents within the lookback window, capped per Source/Ticker.
                var kept = fetched
                    .Where(d => d.PublishedAt is null || d.PublishedAt >= since)
                    .Take(MaxDocsPerSourcePerTicker)
                    .ToList();

                var stored = 0;
                var skipped = 0;
                foreach (var doc in kept)
                {
                    // AC-04: dedup within the Run via the repository.
                    if (await _repository.AddIfNewAsync(doc, ct))
                    {
                        stored++;
                    }
                    else
                    {
                        skipped++;
                    }
                }

                results.Add(new SourceIngestResult
                {
                    Ticker = ticker,
                    Source = source.Kind,
                    Fetched = kept.Count,
                    Stored = stored,
                    Skipped = skipped,
                });

                // AC-07: zero fresh documents is a normal, non-failure result.
                _logger.LogInformation(
                    "Ingested run {RunId} ticker {Ticker} source {Source}: {Stored} stored, {Skipped} skipped",
                    runId, ticker.Value, source.Kind, stored, skipped);
            }
        }

        return new IngestionSummary
        {
            RunId = runId,
            Results = results,
            Gaps = gaps,
        };
    }
}
