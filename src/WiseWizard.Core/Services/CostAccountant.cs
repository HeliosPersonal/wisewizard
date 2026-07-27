namespace WiseWizard.Core.Services;

/// <summary>
/// Per-tier token pricing, expressed in USD per million tokens. Kept in Core so the pure cost
/// math has no Infrastructure dependency; the Anthropic client supplies concrete rates.
/// </summary>
public sealed class TierPricing
{
    public decimal InputPerMillionUsd { get; set; }
    public decimal OutputPerMillionUsd { get; set; }

    /// <summary>True when neither rate is negative (negative pricing would produce negative cost).</summary>
    public bool IsValid() => InputPerMillionUsd >= 0m && OutputPerMillionUsd >= 0m;
}

/// <summary>The token volume observed for one tier of a Run.</summary>
public sealed record TierUsage
{
    public required long InputTokens { get; init; }
    public required long OutputTokens { get; init; }

    public long TotalTokens => InputTokens + OutputTokens;
}

/// <summary>The cost breakdown of a Run across the two tiers.</summary>
public sealed record RunCost
{
    public required decimal CheapUsd { get; init; }
    public required decimal SynthesisUsd { get; init; }

    public decimal TotalUsd => CheapUsd + SynthesisUsd;
}

/// <summary>
/// Pure cost arithmetic for a Run (PRD §6, AC-07). Converts per-tier token usage into USD from the
/// supplied pricing and answers whether a projected total would breach the configured ceiling.
/// Also exposes the cheap-tier token-share helper for the ≥80% NFR.
/// </summary>
public static class CostAccountant
{
    /// <summary>Cost of a single tier's token usage.</summary>
    public static decimal TierCostUsd(TierUsage usage, TierPricing pricing)
    {
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentNullException.ThrowIfNull(pricing);

        return (usage.InputTokens * pricing.InputPerMillionUsd
                + usage.OutputTokens * pricing.OutputPerMillionUsd) / 1_000_000m;
    }

    /// <summary>Total Run cost from both tiers' usage and pricing.</summary>
    public static RunCost Compute(
        TierUsage cheap, TierPricing cheapPricing,
        TierUsage synthesis, TierPricing synthesisPricing)
        => new()
        {
            CheapUsd = TierCostUsd(cheap, cheapPricing),
            SynthesisUsd = TierCostUsd(synthesis, synthesisPricing),
        };

    /// <summary>True when a projected total cost would exceed the configured ceiling (AC-07).</summary>
    public static bool WouldExceedCeiling(decimal projectedTotal, decimal ceiling)
        => projectedTotal > ceiling;

    /// <summary>
    /// Cheap-tier share of total token volume (0..1). Returns 0 when there is no token volume so
    /// the NFR helper never divides by zero.
    /// </summary>
    public static double CheapTierShare(long cheapTokens, long totalTokens)
        => totalTokens <= 0 ? 0d : (double)cheapTokens / totalTokens;
}
