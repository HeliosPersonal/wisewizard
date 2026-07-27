using WiseWizard.Core.Models;

namespace WiseWizard.Core.Services;

/// <summary>The parsed structured output of one cheap-tier extraction result.</summary>
public sealed record ExtractionOutput
{
    /// <summary>Whether the cheap tier judged the document relevant to the Ticker.</summary>
    public required bool Relevant { get; init; }
    public required string Fact { get; init; }
    public required FactSentiment Sentiment { get; init; }
    public required FactMateriality Materiality { get; init; }
}

/// <summary>The parsed structured output of one synthesis-tier result for a Ticker.</summary>
public sealed record SynthesisOutput
{
    public required Signal Signal { get; init; }
    public required string SummaryLine { get; init; }
    public required string FullReasoning { get; init; }

    /// <summary>The document ids the synthesis tier cited as evidence (may be empty → invalid).</summary>
    public required IReadOnlyList<string> CitedDocumentIds { get; init; }
}

/// <summary>
/// Pure prompt construction and structured-output parsing for both tiers of the cascade
/// (seq-nightly-run). Prompts ask the model for a simple line-delimited <c>KEY: value</c> block so
/// parsing is deterministic and testable; parsers are tolerant of malformed output — an
/// unparseable extraction is treated as not-relevant and an unparseable synthesis yields no cited
/// evidence so the evidence guard (AC-05) blocks it.
/// </summary>
public static class PromptBuilder
{
    /// <summary>Builds the cheap-tier extraction prompt for one Raw document and its Ticker.</summary>
    public static string BuildExtractionPrompt(RawDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return $"""
            You are a financial research assistant. Decide whether the document below is relevant to
            the ticker {document.Ticker.Value}, and if so extract a single factual statement.

            Respond with EXACTLY these lines and nothing else:
            RELEVANT: yes|no
            FACT: <one sentence, or NONE if not relevant>
            SENTIMENT: positive|neutral|negative
            MATERIALITY: low|medium|high

            TICKER: {document.Ticker.Value}
            DOCUMENT_ID: {document.DocumentId}
            TITLE: {document.Title}
            CONTENT:
            {document.Content}
            """;
    }

    /// <summary>
    /// Builds the synthesis-tier prompt for one Ticker: its Extracted facts plus a short summary of
    /// the previous Verdict (or a note that there is none), asking for a Signal + summary + reasoning
    /// + the cited document ids.
    /// </summary>
    public static string BuildSynthesisPrompt(
        Ticker ticker, IReadOnlyList<ExtractedFact> facts, Verdict? previous)
    {
        ArgumentNullException.ThrowIfNull(facts);

        var factLines = facts.Count == 0
            ? "(no facts)"
            : string.Join(
                "\n",
                facts.Select(f =>
                    $"- [{f.DocumentId}] ({f.Sentiment.ToString().ToLowerInvariant()}, " +
                    $"{f.Materiality.ToString().ToLowerInvariant()}) {f.Fact}"));

        var priorLine = previous is null
            ? "(no previous verdict)"
            : $"{previous.Signal.ToToken()} — {previous.SummaryLine}";

        return $"""
            You are the synthesis analyst for ticker {ticker.Value}. Using ONLY the extracted facts
            below, produce a single verdict. Cite the DOCUMENT_IDs (in square brackets) you relied on.
            Do not invent evidence. If there are no facts, cite nothing.

            Respond with EXACTLY these lines and nothing else:
            SIGNAL: hold|attention|review
            SUMMARY: <one line>
            REASONING: <one paragraph>
            SOURCES: <comma-separated document ids, or NONE>

            PREVIOUS_VERDICT: {priorLine}
            FACTS:
            {factLines}
            """;
    }

    /// <summary>
    /// Parses a cheap-tier result. Malformed or missing fields yield a not-relevant output so the
    /// document contributes no fact rather than crashing the Run.
    /// </summary>
    public static ExtractionOutput ParseExtraction(string text)
    {
        var fields = ParseFields(text);

        if (!TryParseBool(Get(fields, "RELEVANT"), out var relevant) || !relevant)
        {
            return NotRelevant();
        }

        var fact = Get(fields, "FACT");
        if (string.IsNullOrWhiteSpace(fact) || fact.Trim().Equals("NONE", StringComparison.OrdinalIgnoreCase))
        {
            return NotRelevant();
        }

        if (!TryParseSentiment(Get(fields, "SENTIMENT"), out var sentiment)
            || !TryParseMateriality(Get(fields, "MATERIALITY"), out var materiality))
        {
            return NotRelevant();
        }

        return new ExtractionOutput
        {
            Relevant = true,
            Fact = fact.Trim(),
            Sentiment = sentiment,
            Materiality = materiality,
        };
    }

    /// <summary>
    /// Parses a synthesis-tier result. A missing/invalid Signal defaults to Attention; malformed
    /// output leaves the cited-sources list empty so the evidence guard (AC-05) blocks it.
    /// </summary>
    public static SynthesisOutput ParseSynthesis(string text)
    {
        var fields = ParseFields(text);

        var signal = TryParseSignal(Get(fields, "SIGNAL"), out var s) ? s : Signal.Attention;
        var summary = Get(fields, "SUMMARY")?.Trim() ?? string.Empty;
        var reasoning = Get(fields, "REASONING")?.Trim() ?? string.Empty;
        var sources = ParseSources(Get(fields, "SOURCES"));

        return new SynthesisOutput
        {
            Signal = signal,
            SummaryLine = summary,
            FullReasoning = reasoning,
            CitedDocumentIds = sources,
        };
    }

    private static ExtractionOutput NotRelevant() => new()
    {
        Relevant = false,
        Fact = string.Empty,
        Sentiment = FactSentiment.Neutral,
        Materiality = FactMateriality.Low,
    };

    private static IReadOnlyList<string> ParseSources(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Trim().Equals("NONE", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(id => !id.Equals("NONE", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static Dictionary<string, string> ParseFields(string? text)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text))
        {
            return fields;
        }

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            if (key.Length > 0 && !fields.ContainsKey(key))
            {
                fields[key] = value;
            }
        }

        return fields;
    }

    private static string? Get(IReadOnlyDictionary<string, string> fields, string key)
        => fields.TryGetValue(key, out var v) ? v : null;

    private static bool TryParseBool(string? raw, out bool value)
    {
        switch (raw?.Trim().ToLowerInvariant())
        {
            case "yes" or "true":
                value = true;
                return true;
            case "no" or "false":
                value = false;
                return true;
            default:
                value = false;
                return false;
        }
    }

    private static bool TryParseSentiment(string? raw, out FactSentiment sentiment)
    {
        switch (raw?.Trim().ToLowerInvariant())
        {
            case "positive":
                sentiment = FactSentiment.Positive;
                return true;
            case "neutral":
                sentiment = FactSentiment.Neutral;
                return true;
            case "negative":
                sentiment = FactSentiment.Negative;
                return true;
            default:
                sentiment = FactSentiment.Neutral;
                return false;
        }
    }

    private static bool TryParseMateriality(string? raw, out FactMateriality materiality)
    {
        switch (raw?.Trim().ToLowerInvariant())
        {
            case "low":
                materiality = FactMateriality.Low;
                return true;
            case "medium":
                materiality = FactMateriality.Medium;
                return true;
            case "high":
                materiality = FactMateriality.High;
                return true;
            default:
                materiality = FactMateriality.Low;
                return false;
        }
    }

    private static bool TryParseSignal(string? raw, out Signal signal)
    {
        switch (raw?.Trim().ToLowerInvariant())
        {
            case "hold":
                signal = Signal.Hold;
                return true;
            case "attention":
                signal = Signal.Attention;
                return true;
            case "review":
                signal = Signal.Review;
                return true;
            default:
                signal = Signal.Attention;
                return false;
        }
    }
}
