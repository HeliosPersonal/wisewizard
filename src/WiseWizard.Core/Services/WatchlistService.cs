using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;

namespace WiseWizard.Core.Services;

/// <summary>
/// The domain service behind the Owner's Watchlist commands. Normalizes and validates symbols,
/// enforces the domain invariants (dedup, size cap, note length, owned-Position exclusion) and
/// delegates persistence to <see cref="IWatchlistRepository"/>. Time comes from <see cref="IClock"/>.
/// </summary>
public sealed class WatchlistService(
    IWatchlistRepository watchlist,
    IPositionsRepository positions,
    IClock clock)
{
    /// <summary>Maximum number of Tickers allowed on the Watchlist (PRD NFR §6).</summary>
    public const int MaxSize = 100;

    /// <summary>Maximum allowed length of a note in characters (PRD NFR §6).</summary>
    public const int MaxNoteLength = 280;

    private readonly IWatchlistRepository _watchlist = watchlist;
    private readonly IPositionsRepository _positions = positions;
    private readonly IClock _clock = clock;

    /// <summary>
    /// Adds a Ticker to the Watchlist, enforcing the domain invariants in order:
    /// symbol validity (AC-04), note length, owned-Position exclusion (AC-08), size cap,
    /// and dedup (AC-07). Returns a discriminated result describing the outcome.
    /// </summary>
    public async Task<WatchlistAddResult> AddAsync(string rawSymbol, string? note, CancellationToken ct = default)
    {
        if (!Ticker.TryCreate(rawSymbol, out var ticker))
        {
            return WatchlistAddResult.InvalidSymbol;
        }

        if (note is { Length: > MaxNoteLength })
        {
            return WatchlistAddResult.NoteTooLong;
        }

        // AC-08: a Ticker already held as a Position is not added to the Watchlist.
        var ownedTickers = await _positions.GetTickersAsync(ct);
        if (ownedTickers.Contains(ticker))
        {
            return WatchlistAddResult.OwnedPosition;
        }

        // AC-07: a Ticker already on the Watchlist is an idempotent no-op.
        if (await _watchlist.ContainsAsync(ticker, ct))
        {
            return WatchlistAddResult.AlreadyOnWatchlist;
        }

        // Size cap (PRD NFR §6): refuse an add that would exceed the maximum.
        if (await _watchlist.CountAsync(ct) >= MaxSize)
        {
            return WatchlistAddResult.WatchlistFull;
        }

        var entry = new WatchlistEntry
        {
            Ticker = ticker,
            AddedAt = _clock.UtcNow,
            Note = note,
        };

        // The repository is the storage-level backstop for dedup; it returns false on a race.
        return await _watchlist.AddAsync(entry, ct)
            ? WatchlistAddResult.Added
            : WatchlistAddResult.AlreadyOnWatchlist;
    }

    /// <summary>
    /// Removes a Ticker from the Watchlist. Returns true when a row was removed, false when the
    /// symbol was malformed or the Ticker was not on the Watchlist (AC-05).
    /// </summary>
    public async Task<bool> RemoveAsync(string rawSymbol, CancellationToken ct = default)
    {
        if (!Ticker.TryCreate(rawSymbol, out var ticker))
        {
            return false;
        }

        return await _watchlist.RemoveAsync(ticker, ct);
    }

    /// <summary>Reads every Ticker currently on the Watchlist, ordered by when it was added (AC-02).</summary>
    public Task<IReadOnlyList<WatchlistEntry>> GetAllAsync(CancellationToken ct = default) =>
        _watchlist.GetAllAsync(ct);
}
