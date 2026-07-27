namespace WiseWizard.Core.Models;

/// <summary>
/// The per-Ticker, per-Run conclusion produced by the synthesis-tier model: a Signal,
/// a one-line summary, full reasoning, cited Sources and what changed since the previous
/// Run. Advisory only — never an order. A Verdict with no cited evidence is invalid.
/// </summary>
public sealed record Verdict
{
    public required long RunId { get; init; }
    public required Ticker Ticker { get; init; }
    public required Signal Signal { get; init; }
    public required string SummaryLine { get; init; }
    public required string FullReasoning { get; init; }

    /// <summary>The cited document ids that informed this Verdict. Must contain ≥ 1 entry.</summary>
    public required IReadOnlyList<string> Sources { get; init; }

    /// <summary>What changed vs the previous completed Run's Verdict, or a "new" marker.</summary>
    public required string ChangeFromYesterday { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>True when this Verdict cites at least one Source (the evidence invariant).</summary>
    public bool HasEvidence => Sources.Count > 0;
}
