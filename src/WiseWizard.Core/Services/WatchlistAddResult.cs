namespace WiseWizard.Core.Services;

/// <summary>The outcome kind of an attempt to add a Ticker to the Watchlist.</summary>
public enum WatchlistAddOutcome
{
    /// <summary>The Ticker was recorded on the Watchlist (AC-01).</summary>
    Added,

    /// <summary>The Ticker was already on the Watchlist; no second copy was created (AC-07).</summary>
    AlreadyOnWatchlist,

    /// <summary>The raw symbol was not a well-formed Ticker (AC-04).</summary>
    InvalidSymbol,

    /// <summary>The Watchlist is already at its maximum size (PRD NFR §6).</summary>
    WatchlistFull,

    /// <summary>The supplied note exceeded the maximum allowed length (PRD NFR §6).</summary>
    NoteTooLong,

    /// <summary>
    /// The Ticker is already an owned Position, so it was not added to the Watchlist, avoiding
    /// a redundant Watchlist copy of a held Ticker (AC-08).
    /// </summary>
    AlreadyOwned,
}

/// <summary>
/// The result of <see cref="WatchlistService.AddAsync"/>. Carries the outcome kind and,
/// on a successful add, an informational flag mirroring the outcome for callers that inspect
/// the flag directly.
/// </summary>
public sealed record WatchlistAddResult
{
    public required WatchlistAddOutcome Outcome { get; init; }

    /// <summary>
    /// True when the add was refused because the Ticker is an owned Position (AC-08). Mirrors
    /// <see cref="WatchlistAddOutcome.AlreadyOwned"/> for callers that prefer a boolean check.
    /// </summary>
    public bool AlreadyOwned => Outcome == WatchlistAddOutcome.AlreadyOwned;

    public static readonly WatchlistAddResult Added =
        new() { Outcome = WatchlistAddOutcome.Added };

    public static readonly WatchlistAddResult AlreadyOnWatchlist =
        new() { Outcome = WatchlistAddOutcome.AlreadyOnWatchlist };

    public static readonly WatchlistAddResult InvalidSymbol =
        new() { Outcome = WatchlistAddOutcome.InvalidSymbol };

    public static readonly WatchlistAddResult WatchlistFull =
        new() { Outcome = WatchlistAddOutcome.WatchlistFull };

    public static readonly WatchlistAddResult NoteTooLong =
        new() { Outcome = WatchlistAddOutcome.NoteTooLong };

    public static readonly WatchlistAddResult OwnedPosition =
        new() { Outcome = WatchlistAddOutcome.AlreadyOwned };
}
