using Hangfire;
using WiseWizard.Core.Models;
using WiseWizard.Core.Services;

namespace WiseWizard.Host.Jobs;

/// <summary>
/// Hangfire entry points for the nightly research Run. <see cref="StartAsync"/> runs on the recurring
/// nightly schedule; <see cref="PollAsync"/> runs frequently to advance any in-flight Run's Batch
/// jobs; <see cref="ResumeAsync"/> runs once at startup to recover Runs interrupted by a restart.
/// These methods are thin: they open a scope and delegate to the tested Core services.
/// <para>
/// All three are guarded with <see cref="DisableConcurrentExecutionAttribute"/> so a slow poll can
/// never overlap the next 5-minute tick (or a startup resume) and double-submit a synthesis batch.
/// </para>
/// </summary>
public sealed class NightlyPipelineJob(
    IServiceScopeFactory scopeFactory,
    ILogger<NightlyPipelineJob> logger)
{
    // A job that takes longer than the tick interval skips the overlapping tick rather than
    // racing itself; the lock is held at most this long before the next attempt may proceed.
    private const int LockTimeoutSeconds = 300;

    /// <summary>Starts a nightly Run: ingest the Universe's documents, then kick off the cascade.</summary>
    [DisableConcurrentExecution(LockTimeoutSeconds)]
    public async Task StartAsync(CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var universeProvider = sp.GetRequiredService<UniverseProvider>();
        var orchestrator = sp.GetRequiredService<NightlyRunOrchestrator>();
        var ingestion = sp.GetRequiredService<IngestionService>();

        IReadOnlyList<Ticker> universe = await universeProvider.GetUniverseAsync(ct);
        if (universe.Count == 0)
        {
            logger.LogWarning("Nightly Run skipped: Universe is empty");
            return;
        }

        // 1) Create the Run, 2) collect Raw documents for it, 3) submit the cheap-tier batch so the
        //    extraction step has evidence to work from. Ingestion must precede batch submission.
        var run = await orchestrator.CreateRunAsync(universe, ct);
        var summary = await ingestion.IngestAsync(run.RunId, universe, ct);
        await orchestrator.BeginExtractionAsync(run, ct);

        logger.LogInformation(
            "Nightly Run {RunId} started over {Count} tickers ({Docs} documents ingested)",
            run.RunId, universe.Count, summary.TotalStored);
    }

    /// <summary>Advances every resumable Run by polling its pending Batch job (recurring poll).</summary>
    [DisableConcurrentExecution(LockTimeoutSeconds)]
    public Task PollAsync(CancellationToken ct = default) => AdvanceResumableAsync(ct);

    /// <summary>Recovers Runs left in-flight by a process restart (one-off at startup).</summary>
    [DisableConcurrentExecution(LockTimeoutSeconds)]
    public Task ResumeAsync(CancellationToken ct = default) => AdvanceResumableAsync(ct);

    // Poll and Resume are the same operation (advance every resumable Run); the distinct public
    // names keep the two Hangfire recurring/one-off registrations self-documenting.
    private async Task AdvanceResumableAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<NightlyRunOrchestrator>();
        await orchestrator.ResumeAsync(ct);
    }
}
