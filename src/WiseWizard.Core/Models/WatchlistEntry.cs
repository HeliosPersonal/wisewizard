namespace WiseWizard.Core.Models;

/// <summary>
/// An Owner-curated entry: a Ticker to research but not (yet) owned. Together with the
/// Portfolio Tickers it forms the Universe analyzed each Run.
/// </summary>
public sealed record WatchlistEntry
{
    public required Ticker Ticker { get; init; }
    public required DateTimeOffset AddedAt { get; init; }

    /// <summary>Optional free-text note (≤ 280 chars, enforced by the service).</summary>
    public string? Note { get; init; }
}
