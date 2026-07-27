using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;

namespace WiseWizard.Core.Services;

/// <summary>
/// Resolves the Universe for a Run: the distinct set of Tickers held in the Portfolio ∪ on the
/// Watchlist (data-model.md handoffs). Deduplicates (a Ticker both held and watched appears once)
/// and returns them in a stable, sorted order so a Run is deterministic.
/// </summary>
public sealed class UniverseProvider(
    IPositionsRepository positions,
    IWatchlistRepository watchlist)
{
    private readonly IPositionsRepository _positions = positions;
    private readonly IWatchlistRepository _watchlist = watchlist;

    /// <summary>Reads the union of Portfolio and Watchlist Tickers, distinct and sorted.</summary>
    public async Task<IReadOnlyList<Ticker>> GetUniverseAsync(CancellationToken ct = default)
    {
        var held = await _positions.GetTickersAsync(ct);
        var watched = await _watchlist.GetAllAsync(ct);

        return held
            .Concat(watched.Select(w => w.Ticker))
            .Distinct()
            .OrderBy(t => t.Value, StringComparer.Ordinal)
            .ToList();
    }
}
