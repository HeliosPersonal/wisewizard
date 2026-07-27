using WiseWizard.Core.Abstractions;

namespace WiseWizard.Infrastructure.Llm;

/// <summary>
/// Configuration for the Anthropic Message Batches client: the API key, the model id per tier,
/// and per-model pricing used to convert token counts into USD (ADR-0005). Pricing is expressed
/// in USD per million tokens, matching Anthropic's published rate cards.
/// </summary>
public sealed class AnthropicOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "Anthropic";

    /// <summary>Anthropic API key sent as the <c>x-api-key</c> header.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Model id used for the cheap tier (relevance + fact extraction).</summary>
    public string CheapModel { get; set; } = "claude-haiku-4-5-20251001";

    /// <summary>Model id used for the synthesis tier (per-Ticker Verdict).</summary>
    public string SynthesisModel { get; set; } = "claude-opus-4-8";

    /// <summary>Cheap-tier input token price, USD per million input tokens.</summary>
    public decimal CheapInputPerMillionUsd { get; set; } = 1.00m;

    /// <summary>Cheap-tier output token price, USD per million output tokens.</summary>
    public decimal CheapOutputPerMillionUsd { get; set; } = 5.00m;

    /// <summary>Synthesis-tier input token price, USD per million input tokens.</summary>
    public decimal SynthesisInputPerMillionUsd { get; set; } = 15.00m;

    /// <summary>Synthesis-tier output token price, USD per million output tokens.</summary>
    public decimal SynthesisOutputPerMillionUsd { get; set; } = 75.00m;

    /// <summary>Max output tokens requested per message.</summary>
    public int MaxTokens { get; set; } = 1024;

    /// <summary>Resolves the model id for a tier.</summary>
    public string ModelFor(ModelTier tier) => tier switch
    {
        ModelTier.Cheap => CheapModel,
        ModelTier.Synthesis => SynthesisModel,
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown model tier."),
    };

    /// <summary>
    /// True when the shape is internally consistent: model ids set and pricing/token budget in
    /// range. Wired to the options pipeline via <c>Validate(...).ValidateOnStart()</c> so a bad
    /// value fails fast at startup. The API key is deliberately NOT required here — the Host starts
    /// in a degraded mode without it (the pipeline simply fails until a key is configured).
    /// </summary>
    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(CheapModel)
        && !string.IsNullOrWhiteSpace(SynthesisModel)
        && CheapInputPerMillionUsd >= 0m
        && CheapOutputPerMillionUsd >= 0m
        && SynthesisInputPerMillionUsd >= 0m
        && SynthesisOutputPerMillionUsd >= 0m
        && MaxTokens > 0;
}
