using WiseWizard.Core.Models;
using WiseWizard.Core.Services;

namespace WiseWizard.Core.Tests;

public class PromptBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 0, 0, 0, TimeSpan.Zero);

    private static RawDocument Doc(string id = "doc-1", string ticker = "AAPL")
        => new()
        {
            DocumentId = id,
            RunId = 1,
            Ticker = Ticker.Create(ticker),
            Source = SourceKind.News,
            Url = "https://x/1",
            Title = "Apple beats earnings",
            Content = "Apple reported record revenue.",
            FetchedAt = Now,
            ContentHash = "hash",
        };

    private static ExtractedFact Fact(string docId, string fact = "f")
        => new()
        {
            RunId = 1,
            DocumentId = docId,
            Ticker = Ticker.Create("AAPL"),
            Fact = fact,
            Sentiment = FactSentiment.Positive,
            Materiality = FactMateriality.High,
        };

    [Fact]
    public void Extraction_prompt_includes_ticker_and_document()
    {
        var prompt = PromptBuilder.BuildExtractionPrompt(Doc());

        Assert.Contains("AAPL", prompt);
        Assert.Contains("doc-1", prompt);
        Assert.Contains("Apple reported record revenue.", prompt);
        Assert.Contains("RELEVANT:", prompt);
    }

    [Fact]
    public void Synthesis_prompt_includes_facts_and_previous_summary()
    {
        var prompt = PromptBuilder.BuildSynthesisPrompt(
            Ticker.Create("AAPL"),
            [Fact("doc-1", "revenue up")],
            new Verdict
            {
                RunId = 1,
                Ticker = Ticker.Create("AAPL"),
                Signal = Signal.Hold,
                SummaryLine = "steady",
                FullReasoning = "r",
                Sources = ["doc-0"],
                ChangeFromYesterday = "x",
                CreatedAt = Now,
            });

        Assert.Contains("doc-1", prompt);
        Assert.Contains("revenue up", prompt);
        Assert.Contains("steady", prompt);
        Assert.Contains("SIGNAL:", prompt);
    }

    [Fact]
    public void Synthesis_prompt_marks_no_previous_verdict()
    {
        var prompt = PromptBuilder.BuildSynthesisPrompt(Ticker.Create("AAPL"), [Fact("doc-1")], previous: null);
        Assert.Contains("no previous verdict", prompt);
    }

    [Fact]
    public void Synthesis_prompt_handles_no_facts()
    {
        var prompt = PromptBuilder.BuildSynthesisPrompt(Ticker.Create("AAPL"), [], previous: null);
        Assert.Contains("(no facts)", prompt);
    }

    [Fact]
    public void ParseExtraction_reads_relevant_fact()
    {
        var text = """
            RELEVANT: yes
            FACT: Apple beat earnings estimates.
            SENTIMENT: positive
            MATERIALITY: high
            """;

        var parsed = PromptBuilder.ParseExtraction(text);

        Assert.True(parsed.Relevant);
        Assert.Equal("Apple beat earnings estimates.", parsed.Fact);
        Assert.Equal(FactSentiment.Positive, parsed.Sentiment);
        Assert.Equal(FactMateriality.High, parsed.Materiality);
    }

    [Fact]
    public void ParseExtraction_not_relevant_yields_not_relevant()
    {
        var parsed = PromptBuilder.ParseExtraction("RELEVANT: no\nFACT: NONE");
        Assert.False(parsed.Relevant);
    }

    [Fact]
    public void ParseExtraction_none_fact_yields_not_relevant()
    {
        var parsed = PromptBuilder.ParseExtraction("RELEVANT: yes\nFACT: NONE\nSENTIMENT: positive\nMATERIALITY: low");
        Assert.False(parsed.Relevant);
    }

    [Fact]
    public void ParseExtraction_malformed_is_not_relevant()
    {
        Assert.False(PromptBuilder.ParseExtraction("garbage output with no fields").Relevant);
        Assert.False(PromptBuilder.ParseExtraction("").Relevant);
    }

    [Fact]
    public void ParseExtraction_invalid_enum_is_not_relevant()
    {
        var text = "RELEVANT: yes\nFACT: a fact\nSENTIMENT: bogus\nMATERIALITY: high";
        Assert.False(PromptBuilder.ParseExtraction(text).Relevant);
    }

    [Fact]
    public void ParseExtraction_invalid_materiality_is_not_relevant()
    {
        var text = "RELEVANT: yes\nFACT: a fact\nSENTIMENT: negative\nMATERIALITY: bogus";
        Assert.False(PromptBuilder.ParseExtraction(text).Relevant);
    }

    [Fact]
    public void ParseExtraction_missing_fact_field_is_not_relevant()
    {
        // RELEVANT yes but no FACT line at all → whitespace/null fact branch.
        var parsed = PromptBuilder.ParseExtraction("RELEVANT: yes\nSENTIMENT: positive\nMATERIALITY: high");
        Assert.False(parsed.Relevant);
    }

    [Fact]
    public void ParseFields_ignores_line_with_empty_key()
    {
        // A line starting with ':' has an empty key and must be ignored.
        var text = ": stray\nRELEVANT: yes\nFACT: keep\nSENTIMENT: positive\nMATERIALITY: high";
        var parsed = PromptBuilder.ParseExtraction(text);
        Assert.True(parsed.Relevant);
        Assert.Equal("keep", parsed.Fact);
    }

    [Fact]
    public void ParseSynthesis_reads_all_fields()
    {
        var text = """
            SIGNAL: review
            SUMMARY: Reconsider the position.
            REASONING: Margins are compressing.
            SOURCES: doc-1, doc-2, doc-1
            """;

        var parsed = PromptBuilder.ParseSynthesis(text);

        Assert.Equal(Signal.Review, parsed.Signal);
        Assert.Equal("Reconsider the position.", parsed.SummaryLine);
        Assert.Equal("Margins are compressing.", parsed.FullReasoning);
        Assert.Equal(new[] { "doc-1", "doc-2" }, parsed.CitedDocumentIds);
    }

    [Fact]
    public void ParseSynthesis_none_sources_is_empty()
    {
        var parsed = PromptBuilder.ParseSynthesis("SIGNAL: hold\nSUMMARY: s\nREASONING: r\nSOURCES: NONE");
        Assert.Empty(parsed.CitedDocumentIds);
    }

    [Fact]
    public void ParseSynthesis_missing_sources_is_empty()
    {
        var parsed = PromptBuilder.ParseSynthesis("SIGNAL: hold\nSUMMARY: s\nREASONING: r");
        Assert.Empty(parsed.CitedDocumentIds);
    }

    [Fact]
    public void ParseSynthesis_invalid_signal_defaults_attention()
    {
        var parsed = PromptBuilder.ParseSynthesis("SIGNAL: bogus\nSUMMARY: s\nREASONING: r\nSOURCES: doc-1");
        Assert.Equal(Signal.Attention, parsed.Signal);
    }

    [Fact]
    public void ParseSynthesis_reads_hold_and_attention_signals()
    {
        Assert.Equal(Signal.Hold, PromptBuilder.ParseSynthesis("SIGNAL: hold\nSOURCES: d").Signal);
        Assert.Equal(Signal.Attention, PromptBuilder.ParseSynthesis("SIGNAL: attention\nSOURCES: d").Signal);
    }

    [Fact]
    public void ParseExtraction_reads_neutral_negative_and_medium_low()
    {
        var neutral = PromptBuilder.ParseExtraction("RELEVANT: true\nFACT: x\nSENTIMENT: neutral\nMATERIALITY: medium");
        Assert.Equal(FactSentiment.Neutral, neutral.Sentiment);
        Assert.Equal(FactMateriality.Medium, neutral.Materiality);

        var negative = PromptBuilder.ParseExtraction("RELEVANT: yes\nFACT: x\nSENTIMENT: negative\nMATERIALITY: low");
        Assert.Equal(FactSentiment.Negative, negative.Sentiment);
        Assert.Equal(FactMateriality.Low, negative.Materiality);
    }

    [Fact]
    public void ParseFields_ignores_lines_without_colon_and_duplicate_keys()
    {
        var text = "no colon here\nRELEVANT: yes\nRELEVANT: no\nFACT: keep\nSENTIMENT: positive\nMATERIALITY: high";
        var parsed = PromptBuilder.ParseExtraction(text);

        // First RELEVANT wins (yes), so it is relevant.
        Assert.True(parsed.Relevant);
        Assert.Equal("keep", parsed.Fact);
    }
}
