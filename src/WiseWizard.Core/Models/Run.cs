namespace WiseWizard.Core.Models;

/// <summary>Lifecycle status of a <see cref="Run"/>. Only <see cref="Finished"/> counts as completed.</summary>
public enum RunStatus
{
    Pending,
    Ingesting,
    Extracting,
    Synthesizing,
    Persisting,
    Finished,
    Failed,
}

/// <summary>
/// One complete nightly execution of the research pipeline over the Universe. Holds the
/// durable state and in-flight Batch job ids that make a Run resumable after a restart,
/// plus per-tier cost/token accounting for the cost ceiling.
/// </summary>
public sealed record Run
{
    public long RunId { get; init; }
    public required RunStatus Status { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }

    /// <summary>Map of tier name → Batch job id, read on restart to resume polling.</summary>
    public IReadOnlyDictionary<string, string> BatchIds { get; init; } =
        new Dictionary<string, string>();

    public decimal CostCheapUsd { get; init; }
    public decimal CostSynthesisUsd { get; init; }
    public decimal CostTotalUsd { get; init; }
    public long TokensCheap { get; init; }
    public long TokensTotal { get; init; }
    public string? FailureReason { get; init; }
}
