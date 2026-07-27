using System.Text.Json;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;

namespace WiseWizard.Core.Services;

/// <summary>
/// The nightly Run state machine (seq-nightly-run, seq-batch-poll-resume, seq-run-failure). Drives
/// a Run through explicit persisted status transitions
/// (pending → extracting → synthesizing → persisting → finished, or → failed) over the asynchronous
/// Batch API. Batch waiting is modelled as <see cref="PollAndAdvanceAsync"/>: a single poll that
/// advances one step when the pending batch completes — the same method a Hangfire recurring poll
/// and <see cref="ResumeAsync"/> both call, so a mid-Run restart repeats no completed step and
/// produces no duplicate Verdict (AC-08). Every timestamp comes from <see cref="IClock"/>.
/// </summary>
public sealed class NightlyRunOrchestrator(
    IRunRepository runs,
    IExtractedFactRepository facts,
    IVerdictRepository verdicts,
    ILlmClient llm,
    CheapTierExtractionStep cheapStep,
    SynthesisStep synthesisStep,
    IOwnerNotifier notifier,
    IClock clock,
    PipelineOptions options)
{
    private const string NoEvidenceStateKey = "no_evidence";

    private readonly IRunRepository _runs = runs;
    private readonly IExtractedFactRepository _facts = facts;
    private readonly IVerdictRepository _verdicts = verdicts;
    private readonly ILlmClient _llm = llm;
    private readonly CheapTierExtractionStep _cheapStep = cheapStep;
    private readonly SynthesisStep _synthesisStep = synthesisStep;
    private readonly IOwnerNotifier _notifier = notifier;
    private readonly IClock _clock = clock;
    private readonly PipelineOptions _options = options;

    /// <summary>
    /// Starts a Run over the given Universe: creates the Run, submits the cheap-tier batch and
    /// persists its id, leaving the Run in <see cref="RunStatus.Extracting"/> awaiting a poll.
    /// The Universe is stored in the Run's batch-ids map so a later step knows which Tickers to
    /// synthesize (including those with no evidence).
    /// </summary>
    public async Task<Run> StartRunAsync(IReadOnlyList<Ticker> universe, CancellationToken ct = default)
    {
        var run = await CreateRunAsync(universe, ct);
        return await BeginExtractionAsync(run, ct);
    }

    /// <summary>
    /// Creates a Run in <see cref="RunStatus.Pending"/> and records its Universe, without yet
    /// submitting any batch. Ingestion of the Run's Raw documents happens between this call and
    /// <see cref="BeginExtractionAsync"/>, so the cheap tier has evidence to extract from.
    /// </summary>
    public async Task<Run> CreateRunAsync(IReadOnlyList<Ticker> universe, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(universe);

        var run = await _runs.CreateAsync(
            new Run { Status = RunStatus.Pending, StartedAt = _clock.UtcNow }, ct);

        var state = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [UniverseKey] = SerializeUniverse(universe),
        };

        run = run with { BatchIds = state };
        await _runs.UpdateAsync(run, ct);
        return run;
    }

    /// <summary>
    /// Submits the cheap-tier extraction batch for an already-created, already-ingested Run and moves
    /// it to <see cref="RunStatus.Extracting"/> awaiting a poll.
    /// </summary>
    public async Task<Run> BeginExtractionAsync(Run run, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        var cheapBatchId = await _cheapStep.SubmitAsync(run.RunId, ct);

        var state = new Dictionary<string, string>(run.BatchIds, StringComparer.Ordinal)
        {
            [PipelineOptions.CheapBatchKey] = cheapBatchId,
        };

        run = run with { Status = RunStatus.Extracting, BatchIds = state };
        await _runs.UpdateAsync(run, ct);
        return run;
    }

    /// <summary>
    /// Loads a Run and advances the state machine by one step if its pending batch is ready. Returns
    /// the (possibly advanced) Run. Enforces the wall-clock timeout and batch-failure guard (AC-03)
    /// and the cost ceiling (AC-07); a terminal Run is returned unchanged.
    /// </summary>
    public async Task<Run?> PollAndAdvanceAsync(long runId, CancellationToken ct = default)
    {
        var run = await _runs.GetAsync(runId, ct);
        if (run is null || run.Status is RunStatus.Finished or RunStatus.Failed)
        {
            return run;
        }

        if (_clock.UtcNow - run.StartedAt > _options.MaxWallClock)
        {
            return await FailAsync(run, "Run exceeded the maximum wall-clock and timed out.", ct);
        }

        return run.Status switch
        {
            RunStatus.Extracting => await AdvanceExtractingAsync(run, ct),
            RunStatus.Synthesizing => await AdvanceSynthesizingAsync(run, ct),
            _ => run,
        };
    }

    /// <summary>
    /// Resumes every resumable Run after a restart by polling and advancing each once. Idempotent —
    /// completed extraction is not repeated and Verdicts are upserted by (run_id, ticker) (AC-08).
    /// </summary>
    public async Task ResumeAsync(CancellationToken ct = default)
    {
        var resumable = await _runs.GetResumableAsync(ct);
        foreach (var run in resumable)
        {
            await PollAndAdvanceAsync(run.RunId, ct);
        }
    }

    private async Task<Run?> AdvanceExtractingAsync(Run run, CancellationToken ct)
    {
        if (!run.BatchIds.TryGetValue(PipelineOptions.CheapBatchKey, out var cheapBatchId))
        {
            return await FailAsync(run, "Cheap-tier batch id missing; cannot resume extraction.", ct);
        }

        var status = await _llm.GetBatchStatusAsync(cheapBatchId, ct);
        if (status == BatchStatus.Failed)
        {
            return await FailAsync(run, "Cheap-tier batch failed.", ct);
        }

        if (status == BatchStatus.InProgress)
        {
            return run; // still pending; a later poll will advance.
        }

        var outcome = await _cheapStep.ProcessResultsAsync(run.RunId, cheapBatchId, ct);
        await _facts.AddRangeAsync(outcome.Facts, ct);

        var cheapCost = CostAccountant.TierCostUsd(outcome.Usage, _options.CheapPricing);
        run = run with
        {
            CostCheapUsd = cheapCost,
            CostTotalUsd = cheapCost,
            TokensCheap = outcome.Usage.TotalTokens,
            TokensTotal = outcome.Usage.TotalTokens,
        };

        // AC-07: stop before committing the synthesis tier if the accumulated cost already breaches.
        if (CostAccountant.WouldExceedCeiling(run.CostTotalUsd, _options.CostCeilingUsd))
        {
            await _runs.UpdateAsync(run, ct);
            return await FailAsync(run, "Cost ceiling reached.", ct);
        }

        var universe = DeserializeUniverse(run.BatchIds.GetValueOrDefault(UniverseKey));
        var submission = await _synthesisStep.SubmitAsync(run.RunId, universe, ct);

        var state = new Dictionary<string, string>(run.BatchIds, StringComparer.Ordinal);
        if (submission.BatchId is not null)
        {
            state[PipelineOptions.SynthesisBatchKey] = submission.BatchId;
        }

        state[NoEvidenceStateKey] = SerializeNoEvidence(submission.NoEvidence);

        run = run with { Status = RunStatus.Synthesizing, BatchIds = state };
        await _runs.UpdateAsync(run, ct);

        // When no Ticker had facts there is no synthesis batch — advance immediately.
        if (submission.BatchId is null)
        {
            return await AdvanceSynthesizingAsync(run, ct);
        }

        return run;
    }

    private async Task<Run?> AdvanceSynthesizingAsync(Run run, CancellationToken ct)
    {
        var carried = DeserializeNoEvidence(run.BatchIds.GetValueOrDefault(NoEvidenceStateKey));

        SynthesisOutcome outcome;
        if (run.BatchIds.TryGetValue(PipelineOptions.SynthesisBatchKey, out var synthBatchId))
        {
            var status = await _llm.GetBatchStatusAsync(synthBatchId, ct);
            if (status == BatchStatus.Failed)
            {
                return await FailAsync(run, "Synthesis-tier batch failed.", ct);
            }

            if (status == BatchStatus.InProgress)
            {
                return run;
            }

            outcome = await _synthesisStep.ProcessResultsAsync(run.RunId, synthBatchId, carried, ct);
        }
        else
        {
            // No synthesis batch was submitted (no Ticker had facts) — only the no-evidence records.
            outcome = new SynthesisOutcome
            {
                Verdicts = [],
                NoVerdicts = carried,
                Usage = new TierUsage { InputTokens = 0, OutputTokens = 0 },
            };
        }

        var synthesisCost = CostAccountant.TierCostUsd(outcome.Usage, _options.SynthesisPricing);
        var projectedTotal = run.CostCheapUsd + synthesisCost;

        // AC-07: if adding synthesis cost breaches the ceiling, fail without publishing Verdicts.
        if (CostAccountant.WouldExceedCeiling(projectedTotal, _options.CostCeilingUsd))
        {
            run = run with
            {
                CostSynthesisUsd = synthesisCost,
                CostTotalUsd = projectedTotal,
                TokensTotal = run.TokensCheap + outcome.Usage.TotalTokens,
            };
            await _runs.UpdateAsync(run, ct);
            return await FailAsync(run, "Cost ceiling reached.", ct);
        }

        run = run with
        {
            Status = RunStatus.Persisting,
            CostSynthesisUsd = synthesisCost,
            CostTotalUsd = projectedTotal,
            TokensTotal = run.TokensCheap + outcome.Usage.TotalTokens,
        };
        await _runs.UpdateAsync(run, ct);

        // Persist Verdicts idempotently (AC-08): upsert on (run_id, ticker).
        foreach (var verdict in outcome.Verdicts)
        {
            await _verdicts.UpsertAsync(verdict, ct);
        }

        run = run with { Status = RunStatus.Finished, FinishedAt = _clock.UtcNow };
        await _runs.UpdateAsync(run, ct);
        return run;
    }

    private async Task<Run> FailAsync(Run run, string reason, CancellationToken ct)
    {
        var failed = run with
        {
            Status = RunStatus.Failed,
            FinishedAt = _clock.UtcNow,
            FailureReason = reason,
        };
        await _runs.UpdateAsync(failed, ct);
        await _notifier.NotifyAsync(AlertKind.RunFailed, $"Tonight's Run {run.RunId} failed: {reason}", ct);
        return failed;
    }

    private const string UniverseKey = "universe";

    private static string SerializeUniverse(IReadOnlyList<Ticker> universe)
        => JsonSerializer.Serialize(universe.Select(t => t.Value).ToList());

    private static IReadOnlyList<Ticker> DeserializeUniverse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        var values = JsonSerializer.Deserialize<List<string>>(json) ?? [];
        return values.Select(Ticker.Create).ToList();
    }

    private static string SerializeNoEvidence(IReadOnlyList<NoVerdictRecord> records)
        => JsonSerializer.Serialize(records.Select(r => r.Ticker.Value).ToList());

    private static IReadOnlyList<NoVerdictRecord> DeserializeNoEvidence(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        var values = JsonSerializer.Deserialize<List<string>>(json) ?? [];
        return values
            .Select(v => new NoVerdictRecord { Ticker = Ticker.Create(v), Reason = NoVerdictReason.NoFreshEvidence })
            .ToList();
    }
}
