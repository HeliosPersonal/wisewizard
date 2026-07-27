using WiseWizard.Core.Models;

namespace WiseWizard.Core.Abstractions;

/// <summary>Persistence for the Owner's Watchlist.</summary>
public interface IWatchlistRepository
{
    /// <summary>Adds an entry. Returns false if the Ticker is already on the Watchlist (no duplicate).</summary>
    Task<bool> AddAsync(WatchlistEntry entry, CancellationToken ct = default);

    /// <summary>Removes a Ticker. Returns false if it was not on the Watchlist.</summary>
    Task<bool> RemoveAsync(Ticker ticker, CancellationToken ct = default);

    /// <summary>Reads all Watchlist entries ordered by when they were added.</summary>
    Task<IReadOnlyList<WatchlistEntry>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Reports whether a Ticker is currently on the Watchlist.</summary>
    Task<bool> ContainsAsync(Ticker ticker, CancellationToken ct = default);

    /// <summary>Counts the entries on the Watchlist (for the max-size invariant).</summary>
    Task<int> CountAsync(CancellationToken ct = default);
}
