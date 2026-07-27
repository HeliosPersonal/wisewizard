using WiseWizard.Core.Models;

namespace WiseWizard.Core.Abstractions;

/// <summary>
/// Common shape of a free Source of Raw documents for a Ticker. Concrete Sources (SEC EDGAR,
/// news RSS, market data) implement one of the derived interfaces; adding a Source is a new
/// implementation, not a change to consumers (Open/Closed).
/// </summary>
public interface IDataSource
{
    SourceKind Kind { get; }

    /// <summary>
    /// Fetches Raw documents about the Ticker published within the lookback window. A Source that
    /// is unreachable or rate-limited should surface that as an exception; the caller records the
    /// gap and continues with other Sources.
    /// </summary>
    Task<IReadOnlyList<RawDocument>> FetchAsync(
        Ticker ticker, long runId, DateTimeOffset since, CancellationToken ct = default);
}

/// <summary>SEC EDGAR filings Source (10-K, 10-Q, 8-K, Form 4, ...).</summary>
public interface ISecFilingsSource : IDataSource;

/// <summary>News RSS Source.</summary>
public interface INewsSource : IDataSource;

/// <summary>Market / fundamental data Source.</summary>
public interface IMarketDataSource : IDataSource;
