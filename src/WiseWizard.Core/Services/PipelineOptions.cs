namespace WiseWizard.Core.Services;

/// <summary>
/// Tunable limits for a nightly Run (PRD §6). Defaults match the NFR table: a 2.00 USD per-Run
/// cost ceiling and a 20-hour maximum wall-clock before the Run times out and fails cleanly.
/// Per-tier token pricing is supplied here so the Core cost math stays free of Infrastructure types.
/// </summary>
public sealed class PipelineOptions
{
    /// <summary>Per-Run cost ceiling in USD; a Run projected to exceed it stops and fails (AC-07).</summary>
    public decimal CostCeilingUsd { get; set; } = 2.00m;

    /// <summary>Maximum wall-clock from Run start before it times out and fails (AC-03).</summary>
    public TimeSpan MaxWallClock { get; set; } = TimeSpan.FromHours(20);

    /// <summary>Cheap-tier token pricing used to cost the extraction tier.</summary>
    public TierPricing CheapPricing { get; set; } = new();

    /// <summary>Synthesis-tier token pricing used to cost the synthesis tier.</summary>
    public TierPricing SynthesisPricing { get; set; } = new();

    /// <summary>The tier keys persisted in <c>runs.batch_ids_json</c>.</summary>
    public const string CheapBatchKey = "cheap";
    public const string SynthesisBatchKey = "synthesis";

    /// <summary>
    /// True when the tunable limits are internally consistent. Wired to the options pipeline via
    /// <c>AddOptions().Validate(...).ValidateOnStart()</c> so a bad limit (a non-positive ceiling
    /// that would break the AC-07 guard, a non-positive AC-03 timeout, or negative pricing) fails
    /// fast at host startup rather than silently at run time. Presence of secrets is NOT checked —
    /// the app runs in a degraded mode without them by design.
    /// </summary>
    public bool IsValid() =>
        CostCeilingUsd > 0m
        && MaxWallClock > TimeSpan.Zero
        && CheapPricing is not null && CheapPricing.IsValid()
        && SynthesisPricing is not null && SynthesisPricing.IsValid();
}
