using WiseWizard.Core.Abstractions;
using WiseWizard.Infrastructure.Llm;

namespace WiseWizard.Infrastructure.Tests;

public class AnthropicOptionsTests
{
    private static AnthropicOptions Valid() => new()
    {
        ApiKey = "sk-ant-test",
        CheapModel = "claude-haiku",
        SynthesisModel = "claude-opus",
        CheapInputPerMillionUsd = 1m,
        CheapOutputPerMillionUsd = 5m,
        SynthesisInputPerMillionUsd = 15m,
        SynthesisOutputPerMillionUsd = 75m,
        MaxTokens = 1024,
    };

    [Fact]
    public void IsValid_true_for_well_formed_options()
    {
        Assert.True(Valid().IsValid());
    }

    [Fact]
    public void IsValid_true_with_default_models_and_no_api_key()
    {
        // The API key is deliberately not required — the Host runs degraded without it.
        var o = new AnthropicOptions();
        Assert.True(o.IsValid());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_false_when_cheap_model_blank(string model)
    {
        var o = Valid();
        o.CheapModel = model;
        Assert.False(o.IsValid());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_false_when_synthesis_model_blank(string model)
    {
        var o = Valid();
        o.SynthesisModel = model;
        Assert.False(o.IsValid());
    }

    [Fact]
    public void IsValid_false_when_cheap_input_negative()
    {
        var o = Valid();
        o.CheapInputPerMillionUsd = -1m;
        Assert.False(o.IsValid());
    }

    [Fact]
    public void IsValid_false_when_cheap_output_negative()
    {
        var o = Valid();
        o.CheapOutputPerMillionUsd = -1m;
        Assert.False(o.IsValid());
    }

    [Fact]
    public void IsValid_false_when_synthesis_input_negative()
    {
        var o = Valid();
        o.SynthesisInputPerMillionUsd = -1m;
        Assert.False(o.IsValid());
    }

    [Fact]
    public void IsValid_false_when_synthesis_output_negative()
    {
        var o = Valid();
        o.SynthesisOutputPerMillionUsd = -1m;
        Assert.False(o.IsValid());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void IsValid_false_when_max_tokens_not_positive(int maxTokens)
    {
        var o = Valid();
        o.MaxTokens = maxTokens;
        Assert.False(o.IsValid());
    }

    [Fact]
    public void ModelFor_resolves_each_tier()
    {
        var o = Valid();
        Assert.Equal("claude-haiku", o.ModelFor(ModelTier.Cheap));
        Assert.Equal("claude-opus", o.ModelFor(ModelTier.Synthesis));
    }

    [Fact]
    public void ModelFor_throws_for_unknown_tier()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Valid().ModelFor((ModelTier)999));
    }
}
