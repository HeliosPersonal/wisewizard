using WiseWizard.Core.Services;

namespace WiseWizard.Core.Tests;

public class CostAccountantTests
{
    private static readonly TierPricing CheapPricing = new()
    {
        InputPerMillionUsd = 0.25m,
        OutputPerMillionUsd = 1.25m,
    };

    private static readonly TierPricing SynthesisPricing = new()
    {
        InputPerMillionUsd = 3.00m,
        OutputPerMillionUsd = 15.00m,
    };

    [Fact]
    public void TierCostUsd_prices_input_and_output_tokens()
    {
        var usage = new TierUsage { InputTokens = 1_000_000, OutputTokens = 1_000_000 };

        var cost = CostAccountant.TierCostUsd(usage, CheapPricing);

        Assert.Equal(1.50m, cost);
    }

    [Fact]
    public void Compute_sums_both_tiers()
    {
        var cheap = new TierUsage { InputTokens = 2_000_000, OutputTokens = 0 };
        var synth = new TierUsage { InputTokens = 0, OutputTokens = 100_000 };

        var cost = CostAccountant.Compute(cheap, CheapPricing, synth, SynthesisPricing);

        Assert.Equal(0.50m, cost.CheapUsd);
        Assert.Equal(1.50m, cost.SynthesisUsd);
        Assert.Equal(2.00m, cost.TotalUsd);
    }

    [Fact]
    public void WouldExceedCeiling_true_when_over()
    {
        Assert.True(CostAccountant.WouldExceedCeiling(2.01m, 2.00m));
    }

    [Fact]
    public void WouldExceedCeiling_false_when_exactly_at_ceiling()
    {
        Assert.False(CostAccountant.WouldExceedCeiling(2.00m, 2.00m));
    }

    [Fact]
    public void WouldExceedCeiling_false_when_under()
    {
        Assert.False(CostAccountant.WouldExceedCeiling(1.00m, 2.00m));
    }

    [Fact]
    public void CheapTierShare_computes_ratio()
    {
        Assert.Equal(0.8d, CostAccountant.CheapTierShare(800, 1000), 5);
    }

    [Fact]
    public void CheapTierShare_zero_when_no_tokens()
    {
        Assert.Equal(0d, CostAccountant.CheapTierShare(0, 0));
    }

    [Fact]
    public void TierUsage_total_is_input_plus_output()
    {
        var usage = new TierUsage { InputTokens = 3, OutputTokens = 4 };
        Assert.Equal(7, usage.TotalTokens);
    }
}
