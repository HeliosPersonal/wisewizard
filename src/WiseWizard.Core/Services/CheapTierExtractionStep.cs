using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;

namespace WiseWizard.Core.Services;

/// <summary>The outcome of processing a cheap-tier batch's results.</summary>
public sealed record ExtractionOutcome
{
    /// <summary>The relevant facts extracted from the Run's Raw documents.</summary>
    public required IReadOnlyList<ExtractedFact> Facts { get; init; }

    /// <summary>Cheap-tier token usage, for cost + the cheap-share NFR.</summary>
    public required TierUsage Usage { get; init; }
}

/// <summary>
/// Cheap tier of the cascade (seq-nightly-run): relevance filter + fact extraction over a Run's
/// Raw documents. Submit and result-processing are exposed separately so the orchestrator can
/// persist the batch id, poll, and resume across a restart. Each batch item is correlated by its
/// <c>document_id</c>.
/// </summary>
public sealed class CheapTierExtractionStep(
    ILlmClient llm,
    IRawDocumentRepository rawDocuments)
{
    private readonly ILlmClient _llm = llm;
    private readonly IRawDocumentRepository _rawDocuments = rawDocuments;

    /// <summary>Builds and submits the cheap-tier batch for a Run; returns the provider batch id.</summary>
    public async Task<string> SubmitAsync(long runId, CancellationToken ct = default)
    {
        var documents = await _rawDocuments.GetForRunAsync(runId, null, ct);

        var items = documents
            .Select(d => new BatchRequestItem
            {
                CustomId = d.DocumentId,
                Prompt = PromptBuilder.BuildExtractionPrompt(d),
            })
            .ToList();

        return await _llm.SubmitBatchAsync(ModelTier.Cheap, items, ct);
    }

    /// <summary>
    /// Retrieves and maps a completed cheap-tier batch into <see cref="ExtractedFact"/>s. Each
    /// result is correlated back to its Raw document by <c>custom_id</c>; irrelevant or unparseable
    /// results contribute no fact.
    /// </summary>
    public async Task<ExtractionOutcome> ProcessResultsAsync(
        long runId, string batchId, CancellationToken ct = default)
    {
        var documents = await _rawDocuments.GetForRunAsync(runId, null, ct);
        var byId = documents.ToDictionary(d => d.DocumentId, StringComparer.Ordinal);

        var results = await _llm.GetBatchResultsAsync(batchId, ct);

        var facts = new List<ExtractedFact>();
        long inputTokens = 0;
        long outputTokens = 0;

        foreach (var result in results)
        {
            inputTokens += result.InputTokens;
            outputTokens += result.OutputTokens;

            if (!byId.TryGetValue(result.CustomId, out var document))
            {
                continue;
            }

            var parsed = PromptBuilder.ParseExtraction(result.Text);
            if (!parsed.Relevant)
            {
                continue;
            }

            facts.Add(new ExtractedFact
            {
                RunId = runId,
                DocumentId = document.DocumentId,
                Ticker = document.Ticker,
                Fact = parsed.Fact,
                Sentiment = parsed.Sentiment,
                Materiality = parsed.Materiality,
            });
        }

        return new ExtractionOutcome
        {
            Facts = facts,
            Usage = new TierUsage { InputTokens = inputTokens, OutputTokens = outputTokens },
        };
    }
}
