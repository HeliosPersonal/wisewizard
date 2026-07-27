using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;

namespace WiseWizard.Core.Services;

/// <summary>Why a Ticker produced no persisted Verdict this Run.</summary>
public enum NoVerdictReason
{
    /// <summary>No fresh Raw documents were collected for the Ticker this Run (AC-09).</summary>
    NoFreshEvidence,

    /// <summary>The synthesis conclusion cited no document as evidence, so it is invalid (AC-05).</summary>
    NoCitableEvidence,
}

/// <summary>A Ticker that did not yield a Verdict, with the recorded reason.</summary>
public sealed record NoVerdictRecord
{
    public required Ticker Ticker { get; init; }
    public required NoVerdictReason Reason { get; init; }
}

/// <summary>The outcome of processing a synthesis-tier batch's results.</summary>
public sealed record SynthesisOutcome
{
    /// <summary>The valid, evidence-backed Verdicts to persist.</summary>
    public required IReadOnlyList<Verdict> Verdicts { get; init; }

    /// <summary>Tickers recorded as having no persistable Verdict (AC-05 / AC-09).</summary>
    public required IReadOnlyList<NoVerdictRecord> NoVerdicts { get; init; }

    /// <summary>Synthesis-tier token usage, for cost accounting.</summary>
    public required TierUsage Usage { get; init; }
}

/// <summary>
/// Synthesis tier of the cascade (seq-nightly-run): one per-Ticker Verdict from the Ticker's
/// Extracted facts + the previous Verdict summary. Submit and result-processing are separate so the
/// orchestrator can persist the batch id, poll, and resume. Enforces the evidence guard: a Ticker
/// with no facts is not submitted and is recorded as "no fresh evidence" (AC-09); a conclusion that
/// cites no document is blocked and recorded as "no citable evidence" (AC-05). Each batch item is
/// correlated by the Ticker symbol.
/// </summary>
public sealed class SynthesisStep(
    ILlmClient llm,
    IExtractedFactRepository facts,
    IVerdictRepository verdicts,
    IClock clock)
{
    private readonly ILlmClient _llm = llm;
    private readonly IExtractedFactRepository _facts = facts;
    private readonly IVerdictRepository _verdicts = verdicts;
    private readonly IClock _clock = clock;

    /// <summary>
    /// Builds and submits the synthesis batch for the tickers that have at least one Extracted fact.
    /// Returns the batch id and the tickers with no facts (recorded as "no fresh evidence").
    /// A batch id of null means no ticker had facts, so no batch was submitted.
    /// </summary>
    public async Task<SynthesisSubmission> SubmitAsync(
        long runId, IReadOnlyList<Ticker> universe, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(universe);

        var items = new List<BatchRequestItem>();
        var noEvidence = new List<NoVerdictRecord>();

        foreach (var ticker in universe)
        {
            var tickerFacts = await _facts.GetForRunTickerAsync(runId, ticker, ct);
            if (tickerFacts.Count == 0)
            {
                noEvidence.Add(new NoVerdictRecord { Ticker = ticker, Reason = NoVerdictReason.NoFreshEvidence });
                continue;
            }

            var previous = await _verdicts.GetPreviousAsync(ticker, runId, ct);
            items.Add(new BatchRequestItem
            {
                CustomId = ticker.Value,
                Prompt = PromptBuilder.BuildSynthesisPrompt(ticker, tickerFacts, previous),
            });
        }

        var batchId = items.Count == 0
            ? null
            : await _llm.SubmitBatchAsync(ModelTier.Synthesis, items, ct);

        return new SynthesisSubmission { BatchId = batchId, NoEvidence = noEvidence };
    }

    /// <summary>
    /// Retrieves and maps a completed synthesis batch into Verdicts. A conclusion citing zero of the
    /// Ticker's actual facts' document ids is blocked (AC-05); citations are intersected with the
    /// Ticker's real facts so the model cannot invent evidence.
    /// </summary>
    public async Task<SynthesisOutcome> ProcessResultsAsync(
        long runId, string batchId, IReadOnlyList<NoVerdictRecord> carried, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(carried);

        var results = await _llm.GetBatchResultsAsync(batchId, ct);

        var built = new List<Verdict>();
        var noVerdicts = new List<NoVerdictRecord>(carried);
        long inputTokens = 0;
        long outputTokens = 0;

        foreach (var result in results)
        {
            inputTokens += result.InputTokens;
            outputTokens += result.OutputTokens;

            if (!Ticker.TryCreate(result.CustomId, out var ticker))
            {
                continue;
            }

            var tickerFacts = await _facts.GetForRunTickerAsync(runId, ticker, ct);
            var validDocs = tickerFacts.Select(f => f.DocumentId).ToHashSet(StringComparer.Ordinal);

            var parsed = PromptBuilder.ParseSynthesis(result.Text);
            var sources = parsed.CitedDocumentIds.Where(validDocs.Contains).Distinct(StringComparer.Ordinal).ToList();

            if (sources.Count == 0)
            {
                // AC-05: no citable evidence — block it, do not persist an unsupported Verdict.
                noVerdicts.Add(new NoVerdictRecord { Ticker = ticker, Reason = NoVerdictReason.NoCitableEvidence });
                continue;
            }

            var previous = await _verdicts.GetPreviousAsync(ticker, runId, ct);

            built.Add(new Verdict
            {
                RunId = runId,
                Ticker = ticker,
                Signal = parsed.Signal,
                SummaryLine = parsed.SummaryLine,
                FullReasoning = parsed.FullReasoning,
                Sources = sources,
                ChangeFromYesterday = DeltaComputer.Compute(parsed, previous),
                CreatedAt = _clock.UtcNow,
            });
        }

        return new SynthesisOutcome
        {
            Verdicts = built,
            NoVerdicts = noVerdicts,
            Usage = new TierUsage { InputTokens = inputTokens, OutputTokens = outputTokens },
        };
    }
}

/// <summary>The result of submitting the synthesis tier: the batch id (if any) + no-evidence tickers.</summary>
public sealed record SynthesisSubmission
{
    /// <summary>The provider batch id, or null when no Ticker had facts to synthesize.</summary>
    public required string? BatchId { get; init; }

    /// <summary>Tickers with no fresh facts, recorded as "no fresh evidence" (AC-09).</summary>
    public required IReadOnlyList<NoVerdictRecord> NoEvidence { get; init; }
}
