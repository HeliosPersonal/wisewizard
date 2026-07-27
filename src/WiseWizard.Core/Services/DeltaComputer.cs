using WiseWizard.Core.Models;

namespace WiseWizard.Core.Services;

/// <summary>
/// Pure computation of a Verdict's <c>change_from_yesterday</c> text (AC-02, AC-06). With no
/// previous Verdict the Ticker is marked new; otherwise the delta states whether the Signal moved
/// and always carries the current one-line summary so the Owner can scan what changed.
/// </summary>
public static class DeltaComputer
{
    /// <summary>The marker used when a Ticker has no Verdict from any previous completed Run.</summary>
    public const string NewMarker = "New this run — no previous verdict.";

    /// <summary>Builds the delta text for a newly synthesized Ticker against its previous Verdict.</summary>
    public static string Compute(SynthesisOutput current, Verdict? previous)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (previous is null)
        {
            return NewMarker;
        }

        if (previous.Signal != current.Signal)
        {
            return $"Signal changed {previous.Signal.ToToken()} → {current.Signal.ToToken()}: {current.SummaryLine}";
        }

        return $"Signal unchanged ({current.Signal.ToToken()}): {current.SummaryLine}";
    }
}
