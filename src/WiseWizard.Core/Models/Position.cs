namespace WiseWizard.Core.Models;

/// <summary>
/// A holding the Owner currently owns in the brokerage account: a Ticker with a quantity,
/// average cost, market value and unrealized P&amp;L. Read-only — WiseWizard never places orders.
/// Part of a snapshot; every Position in one snapshot shares the same <see cref="AsOf"/>.
/// </summary>
public sealed record Position
{
    public required Ticker Ticker { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal AvgCost { get; init; }
    public required decimal MarketValue { get; init; }
    public required decimal UnrealizedPnl { get; init; }
    public string Currency { get; init; } = "USD";

    /// <summary>ISO-8601 UTC instant the snapshot was read.</summary>
    public required DateTimeOffset AsOf { get; init; }
}
