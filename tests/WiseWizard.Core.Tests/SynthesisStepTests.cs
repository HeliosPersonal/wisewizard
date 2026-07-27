using NSubstitute;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;
using WiseWizard.Core.Services;

namespace WiseWizard.Core.Tests;

public class SynthesisStepTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 0, 0, 0, TimeSpan.Zero);

    private static ExtractedFact Fact(string docId, string ticker = "AAPL")
        => new()
        {
            RunId = 1,
            DocumentId = docId,
            Ticker = Ticker.Create(ticker),
            Fact = "some fact",
            Sentiment = FactSentiment.Positive,
            Materiality = FactMateriality.High,
        };

    private static IClock ClockAt(DateTimeOffset now)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(now);
        return clock;
    }

    [Fact]
    public async Task Submit_skips_tickers_without_facts_and_records_no_fresh_evidence()
    {
        var facts = Substitute.For<IExtractedFactRepository>();
        facts.GetForRunTickerAsync(1, Ticker.Create("AAPL"), Arg.Any<CancellationToken>())
            .Returns([Fact("d1")]);
        facts.GetForRunTickerAsync(1, Ticker.Create("MSFT"), Arg.Any<CancellationToken>())
            .Returns([]);

        var verdicts = Substitute.For<IVerdictRepository>();
        verdicts.GetPreviousAsync(Arg.Any<Ticker>(), 1, Arg.Any<CancellationToken>()).Returns((Verdict?)null);

        var llm = Substitute.For<ILlmClient>();
        llm.SubmitBatchAsync(ModelTier.Synthesis, Arg.Any<IReadOnlyList<BatchRequestItem>>(), Arg.Any<CancellationToken>())
            .Returns("batch-s");

        var step = new SynthesisStep(llm, facts, verdicts, ClockAt(Now));

        var submission = await step.SubmitAsync(1, [Ticker.Create("AAPL"), Ticker.Create("MSFT")]);

        Assert.Equal("batch-s", submission.BatchId);
        var noEv = Assert.Single(submission.NoEvidence);
        Assert.Equal(Ticker.Create("MSFT"), noEv.Ticker);
        Assert.Equal(NoVerdictReason.NoFreshEvidence, noEv.Reason);

        // AC-09: MSFT was NOT submitted; only AAPL had facts.
        await llm.Received(1).SubmitBatchAsync(
            ModelTier.Synthesis,
            Arg.Is<IReadOnlyList<BatchRequestItem>>(i => i != null && i.Count == 1 && i[0].CustomId == "AAPL"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_returns_null_batch_when_no_ticker_has_facts()
    {
        var facts = Substitute.For<IExtractedFactRepository>();
        facts.GetForRunTickerAsync(1, Arg.Any<Ticker>(), Arg.Any<CancellationToken>()).Returns([]);
        var verdicts = Substitute.For<IVerdictRepository>();
        var llm = Substitute.For<ILlmClient>();

        var submission = await new SynthesisStep(llm, facts, verdicts, ClockAt(Now))
            .SubmitAsync(1, [Ticker.Create("AAPL")]);

        Assert.Null(submission.BatchId);
        Assert.Single(submission.NoEvidence);
        await llm.DidNotReceive().SubmitBatchAsync(
            Arg.Any<ModelTier>(), Arg.Any<IReadOnlyList<BatchRequestItem>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessResults_builds_verdict_with_delta_and_valid_sources()
    {
        var facts = Substitute.For<IExtractedFactRepository>();
        facts.GetForRunTickerAsync(2, Ticker.Create("AAPL"), Arg.Any<CancellationToken>())
            .Returns([Fact("d1"), Fact("d2")]);

        var verdicts = Substitute.For<IVerdictRepository>();
        verdicts.GetPreviousAsync(Ticker.Create("AAPL"), 2, Arg.Any<CancellationToken>())
            .Returns(new Verdict
            {
                RunId = 1,
                Ticker = Ticker.Create("AAPL"),
                Signal = Signal.Hold,
                SummaryLine = "prev",
                FullReasoning = "r",
                Sources = ["d0"],
                ChangeFromYesterday = "x",
                CreatedAt = Now,
            });

        var llm = Substitute.For<ILlmClient>();
        llm.GetBatchResultsAsync("bs", Arg.Any<CancellationToken>()).Returns(new[]
        {
            new BatchResultItem
            {
                CustomId = "AAPL",
                Text = "SIGNAL: review\nSUMMARY: reconsider\nREASONING: because\nSOURCES: d1, d99",
                InputTokens = 100,
                OutputTokens = 50,
            },
        });

        var step = new SynthesisStep(llm, facts, verdicts, ClockAt(Now));
        var outcome = await step.ProcessResultsAsync(2, "bs", []);

        var verdict = Assert.Single(outcome.Verdicts);
        Assert.Equal(Signal.Review, verdict.Signal);
        // d99 is not a real fact for AAPL, so it is filtered out — only d1 remains.
        Assert.Equal(new[] { "d1" }, verdict.Sources);
        Assert.Contains("hold", verdict.ChangeFromYesterday);
        Assert.Contains("review", verdict.ChangeFromYesterday);
        Assert.Equal(Now, verdict.CreatedAt);
        Assert.Equal(150, outcome.Usage.TotalTokens);
        Assert.Empty(outcome.NoVerdicts);
    }

    [Fact]
    public async Task ProcessResults_blocks_conclusion_with_no_citable_evidence()
    {
        // AC-05: cites only a document that is not among the ticker's real facts → blocked.
        var facts = Substitute.For<IExtractedFactRepository>();
        facts.GetForRunTickerAsync(1, Ticker.Create("AAPL"), Arg.Any<CancellationToken>())
            .Returns([Fact("d1")]);

        var verdicts = Substitute.For<IVerdictRepository>();

        var llm = Substitute.For<ILlmClient>();
        llm.GetBatchResultsAsync("bs", Arg.Any<CancellationToken>()).Returns(new[]
        {
            new BatchResultItem
            {
                CustomId = "AAPL",
                Text = "SIGNAL: review\nSUMMARY: s\nREASONING: r\nSOURCES: NONE",
                InputTokens = 1,
                OutputTokens = 1,
            },
        });

        var outcome = await new SynthesisStep(llm, facts, verdicts, ClockAt(Now))
            .ProcessResultsAsync(1, "bs", []);

        Assert.Empty(outcome.Verdicts);
        var blocked = Assert.Single(outcome.NoVerdicts);
        Assert.Equal(NoVerdictReason.NoCitableEvidence, blocked.Reason);
    }

    [Fact]
    public async Task ProcessResults_carries_prior_no_evidence_records()
    {
        var facts = Substitute.For<IExtractedFactRepository>();
        facts.GetForRunTickerAsync(1, Ticker.Create("AAPL"), Arg.Any<CancellationToken>())
            .Returns([Fact("d1")]);
        var verdicts = Substitute.For<IVerdictRepository>();
        verdicts.GetPreviousAsync(Arg.Any<Ticker>(), 1, Arg.Any<CancellationToken>()).Returns((Verdict?)null);

        var llm = Substitute.For<ILlmClient>();
        llm.GetBatchResultsAsync("bs", Arg.Any<CancellationToken>()).Returns(new[]
        {
            new BatchResultItem
            {
                CustomId = "AAPL",
                Text = "SIGNAL: hold\nSUMMARY: s\nREASONING: r\nSOURCES: d1",
                InputTokens = 1,
                OutputTokens = 1,
            },
        });

        var carried = new[]
        {
            new NoVerdictRecord { Ticker = Ticker.Create("MSFT"), Reason = NoVerdictReason.NoFreshEvidence },
        };

        var outcome = await new SynthesisStep(llm, facts, verdicts, ClockAt(Now))
            .ProcessResultsAsync(1, "bs", carried);

        Assert.Single(outcome.Verdicts);
        Assert.Contains(outcome.NoVerdicts, r => r.Ticker == Ticker.Create("MSFT"));
    }

    [Fact]
    public async Task ProcessResults_ignores_result_with_invalid_ticker_custom_id()
    {
        var facts = Substitute.For<IExtractedFactRepository>();
        var verdicts = Substitute.For<IVerdictRepository>();
        var llm = Substitute.For<ILlmClient>();
        llm.GetBatchResultsAsync("bs", Arg.Any<CancellationToken>()).Returns(new[]
        {
            new BatchResultItem { CustomId = "", Text = "SIGNAL: hold\nSOURCES: d1", InputTokens = 4, OutputTokens = 0 },
        });

        var outcome = await new SynthesisStep(llm, facts, verdicts, ClockAt(Now))
            .ProcessResultsAsync(1, "bs", []);

        Assert.Empty(outcome.Verdicts);
        Assert.Equal(4, outcome.Usage.InputTokens);
    }
}
