namespace WiseWizard.Core.Abstractions;

/// <summary>A single request within a Batch job, correlated by <see cref="CustomId"/>.</summary>
public sealed record BatchRequestItem
{
    public required string CustomId { get; init; }
    public required string Prompt { get; init; }
}

/// <summary>A single result within a completed Batch job, correlated by <see cref="CustomId"/>.</summary>
public sealed record BatchResultItem
{
    public required string CustomId { get; init; }
    public required string Text { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
}

/// <summary>Lifecycle of a submitted Batch job.</summary>
public enum BatchStatus
{
    InProgress,
    Completed,
    Failed,
}

/// <summary>The model tier used for a Batch job.</summary>
public enum ModelTier
{
    /// <summary>High-volume cheap tier for relevance filtering and fact extraction.</summary>
    Cheap,

    /// <summary>Low-volume synthesis tier for per-Ticker Verdicts.</summary>
    Synthesis,
}

/// <summary>
/// Provider-agnostic access to the model cascade over an asynchronous Batch API. Callers submit
/// a batch, persist the returned id, poll for completion, then retrieve results — so a Run can
/// resume polling after a process restart (ADR-0004, ADR-0005).
/// </summary>
public interface ILlmClient
{
    /// <summary>Submits a batch of requests to the given tier and returns the provider batch id.</summary>
    Task<string> SubmitBatchAsync(ModelTier tier, IReadOnlyList<BatchRequestItem> items, CancellationToken ct = default);

    /// <summary>Polls the status of a previously submitted batch.</summary>
    Task<BatchStatus> GetBatchStatusAsync(string batchId, CancellationToken ct = default);

    /// <summary>Retrieves the results of a completed batch.</summary>
    Task<IReadOnlyList<BatchResultItem>> GetBatchResultsAsync(string batchId, CancellationToken ct = default);
}
