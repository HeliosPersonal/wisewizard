using WiseWizard.Core.Services;

namespace WiseWizard.Core.Tests;

public class PipelineOptionsTests
{
    private static PipelineOptions Valid() => new()
    {
        CostCeilingUsd = 2.00m,
        MaxWallClock = TimeSpan.FromHours(20),
        CheapPricing = new TierPricing { InputPerMillionUsd = 1m, OutputPerMillionUsd = 5m },
        SynthesisPricing = new TierPricing { InputPerMillionUsd = 15m, OutputPerMillionUsd = 75m },
    };

    [Fact]
    public void IsValid_true_for_well_formed_options()
    {
        Assert.True(Valid().IsValid());
    }

    [Fact]
    public void IsValid_defaults_are_valid()
    {
        // Parameterless defaults (2.00 ceiling, 20h, zero pricing) are internally consistent.
        Assert.True(new PipelineOptions().IsValid());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void IsValid_false_when_cost_ceiling_not_positive(decimal ceiling)
    {
        var o = Valid();
        o.CostCeilingUsd = ceiling;
        Assert.False(o.IsValid());
    }

    [Fact]
    public void IsValid_false_when_wall_clock_not_positive()
    {
        var o = Valid();
        o.MaxWallClock = TimeSpan.Zero;
        Assert.False(o.IsValid());
    }

    [Fact]
    public void IsValid_false_when_cheap_pricing_negative()
    {
        var o = Valid();
        o.CheapPricing = new TierPricing { InputPerMillionUsd = -1m, OutputPerMillionUsd = 5m };
        Assert.False(o.IsValid());
    }

    [Fact]
    public void IsValid_false_when_synthesis_pricing_negative()
    {
        var o = Valid();
        o.SynthesisPricing = new TierPricing { InputPerMillionUsd = 15m, OutputPerMillionUsd = -5m };
        Assert.False(o.IsValid());
    }

    [Fact]
    public void IsValid_false_when_pricing_null()
    {
        var o = Valid();
        o.CheapPricing = null!;
        Assert.False(o.IsValid());
    }
}

public class TierPricingTests
{
    [Fact]
    public void IsValid_true_when_both_rates_non_negative()
    {
        Assert.True(new TierPricing { InputPerMillionUsd = 0m, OutputPerMillionUsd = 5m }.IsValid());
    }

    [Fact]
    public void IsValid_false_when_input_negative()
    {
        Assert.False(new TierPricing { InputPerMillionUsd = -1m, OutputPerMillionUsd = 5m }.IsValid());
    }

    [Fact]
    public void IsValid_false_when_output_negative()
    {
        Assert.False(new TierPricing { InputPerMillionUsd = 1m, OutputPerMillionUsd = -5m }.IsValid());
    }
}
