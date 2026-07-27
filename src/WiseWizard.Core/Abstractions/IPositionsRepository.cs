using WiseWizard.Core.Models;

namespace WiseWizard.Core.Abstractions;

/// <summary>
/// Persistence for the Portfolio snapshot. The snapshot is overwritten wholesale on each
/// successful refresh (delete-all-then-insert in one transaction); a failed refresh leaves
/// the previous snapshot untouched.
/// </summary>
public interface IPositionsRepository
{
    /// <summary>Replaces the entire snapshot atomically with the given Positions.</summary>
    Task ReplaceSnapshotAsync(IReadOnlyList<Position> positions, CancellationToken ct = default);

    /// <summary>Reads the current Portfolio snapshot.</summary>
    Task<IReadOnlyList<Position>> GetCurrentAsync(CancellationToken ct = default);

    /// <summary>Reads the distinct Tickers currently held, contributing to the Universe.</summary>
    Task<IReadOnlyList<Ticker>> GetTickersAsync(CancellationToken ct = default);
}
