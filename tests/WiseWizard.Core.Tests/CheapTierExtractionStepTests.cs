using NSubstitute;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;
using WiseWizard.Core.Services;

namespace WiseWizard.Core.Tests;

public class CheapTierExtractionStepTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 0, 0, 0, TimeSpan.Zero);

    private static RawDocument Doc(string id, string ticker)
        => new()
        {
            DocumentId = id,
            RunId = 1,
            Ticker = Ticker.Create(ticker),
            Source = SourceKind.News,
            Title = "T",
            Content = "C",
            FetchedAt = Now,
            ContentHash = "h",
        };

    [Fact]
    public async Task Submit_builds_one_item_per_document_and_returns_batch_id()
    {
        var raw = Substitute.For<IRawDocumentRepository>();
        raw.GetForRunAsync(1, null, Arg.Any<CancellationToken>())
            .Returns([Doc("d1", "AAPL"), Doc("d2", "MSFT")]);

        var llm = Substitute.For<ILlmClient>();
        llm.SubmitBatchAsync(ModelTier.Cheap, Arg.Any<IReadOnlyList<BatchRequestItem>>(), Arg.Any<CancellationToken>())
            .Returns("batch-cheap-1");

        var step = new CheapTierExtractionStep(llm, raw);
        var id = await step.SubmitAsync(1);

        Assert.Equal("batch-cheap-1", id);
        await llm.Received(1).SubmitBatchAsync(
            ModelTier.Cheap,
            Arg.Is<IReadOnlyList<BatchRequestItem>>(items =>
                items != null && items.Count == 2 && items[0].CustomId == "d1" && items[1].CustomId == "d2"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessResults_maps_relevant_facts_and_sums_tokens()
    {
        var raw = Substitute.For<IRawDocumentRepository>();
        raw.GetForRunAsync(1, null, Arg.Any<CancellationToken>())
            .Returns([Doc("d1", "AAPL"), Doc("d2", "MSFT")]);

        var llm = Substitute.For<ILlmClient>();
        llm.GetBatchResultsAsync("b", Arg.Any<CancellationToken>()).Returns(new[]
        {
            new BatchResultItem
            {
                CustomId = "d1",
                Text = "RELEVANT: yes\nFACT: Apple did well.\nSENTIMENT: positive\nMATERIALITY: high",
                InputTokens = 10,
                OutputTokens = 5,
            },
            new BatchResultItem
            {
                CustomId = "d2",
                Text = "RELEVANT: no\nFACT: NONE",
                InputTokens = 3,
                OutputTokens = 1,
            },
        });

        var step = new CheapTierExtractionStep(llm, raw);
        var outcome = await step.ProcessResultsAsync(1, "b");

        var fact = Assert.Single(outcome.Facts);
        Assert.Equal("d1", fact.DocumentId);
        Assert.Equal(Ticker.Create("AAPL"), fact.Ticker);
        Assert.Equal("Apple did well.", fact.Fact);
        Assert.Equal(13, outcome.Usage.InputTokens);
        Assert.Equal(6, outcome.Usage.OutputTokens);
    }

    [Fact]
    public async Task ProcessResults_ignores_unknown_custom_id()
    {
        var raw = Substitute.For<IRawDocumentRepository>();
        raw.GetForRunAsync(1, null, Arg.Any<CancellationToken>()).Returns([Doc("d1", "AAPL")]);

        var llm = Substitute.For<ILlmClient>();
        llm.GetBatchResultsAsync("b", Arg.Any<CancellationToken>()).Returns(new[]
        {
            new BatchResultItem
            {
                CustomId = "unknown",
                Text = "RELEVANT: yes\nFACT: x\nSENTIMENT: positive\nMATERIALITY: high",
                InputTokens = 2,
                OutputTokens = 2,
            },
        });

        var outcome = await new CheapTierExtractionStep(llm, raw).ProcessResultsAsync(1, "b");

        Assert.Empty(outcome.Facts);
        Assert.Equal(2, outcome.Usage.InputTokens);
    }
}
