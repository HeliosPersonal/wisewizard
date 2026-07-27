using WiseWizard.Core.Models;
using WiseWizard.Infrastructure.Persistence;

namespace WiseWizard.Infrastructure.Tests;

public class ExtractedFactRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    private static async Task<long> SeedRunAsync(TestDatabase db)
    {
        var run = await new RunRepository(db).CreateAsync(
            new Run { Status = RunStatus.Extracting, StartedAt = Now });
        return run.RunId;
    }

    private static async Task SeedDocAsync(TestDatabase db, long runId, string docId, string ticker)
    {
        var raw = new RawDocumentRepository(db);
        await raw.AddIfNewAsync(new RawDocument
        {
            DocumentId = docId,
            RunId = runId,
            Ticker = Ticker.Create(ticker),
            Source = SourceKind.News,
            Title = "T-" + docId,
            Content = "C-" + docId,
            FetchedAt = Now,
            ContentHash = "h-" + docId,
        });
    }

    private static ExtractedFact Fact(long runId, string docId, string ticker, FactSentiment s, FactMateriality m)
        => new()
        {
            RunId = runId,
            DocumentId = docId,
            Ticker = Ticker.Create(ticker),
            Fact = "fact about " + ticker,
            Sentiment = s,
            Materiality = m,
        };

    [Fact]
    public async Task AddRange_then_get_round_trips_facts_with_enum_mapping()
    {
        await using var db = await TestDatabase.CreateAsync();
        var runId = await SeedRunAsync(db);
        await SeedDocAsync(db, runId, "d1", "AAPL");
        await SeedDocAsync(db, runId, "d2", "AAPL");
        var repo = new ExtractedFactRepository(db);

        await repo.AddRangeAsync(
        [
            Fact(runId, "d1", "AAPL", FactSentiment.Positive, FactMateriality.High),
            Fact(runId, "d2", "AAPL", FactSentiment.Negative, FactMateriality.Low),
        ]);

        var loaded = await repo.GetForRunTickerAsync(runId, Ticker.Create("AAPL"));

        Assert.Equal(2, loaded.Count);
        Assert.Equal(FactSentiment.Positive, loaded[0].Sentiment);
        Assert.Equal(FactMateriality.High, loaded[0].Materiality);
        Assert.Equal(FactSentiment.Negative, loaded[1].Sentiment);
        Assert.Equal(FactMateriality.Low, loaded[1].Materiality);
        Assert.All(loaded, f => Assert.True(f.Id > 0));
    }

    [Fact]
    public async Task AddRange_with_empty_list_is_noop()
    {
        await using var db = await TestDatabase.CreateAsync();
        var runId = await SeedRunAsync(db);
        var repo = new ExtractedFactRepository(db);

        await repo.AddRangeAsync([]);

        Assert.Empty(await repo.GetForRunTickerAsync(runId, Ticker.Create("AAPL")));
    }

    [Fact]
    public async Task Get_filters_by_run_and_ticker()
    {
        await using var db = await TestDatabase.CreateAsync();
        var runId = await SeedRunAsync(db);
        await SeedDocAsync(db, runId, "d1", "AAPL");
        await SeedDocAsync(db, runId, "d2", "MSFT");
        var repo = new ExtractedFactRepository(db);

        await repo.AddRangeAsync(
        [
            Fact(runId, "d1", "AAPL", FactSentiment.Neutral, FactMateriality.Medium),
            Fact(runId, "d2", "MSFT", FactSentiment.Neutral, FactMateriality.Medium),
        ]);

        var apple = await repo.GetForRunTickerAsync(runId, Ticker.Create("AAPL"));
        var msft = await repo.GetForRunTickerAsync(runId, Ticker.Create("MSFT"));

        Assert.Single(apple);
        Assert.Single(msft);
        Assert.Equal("d1", apple[0].DocumentId);
        Assert.Equal("d2", msft[0].DocumentId);
    }

    [Fact]
    public async Task Get_empty_when_no_facts()
    {
        await using var db = await TestDatabase.CreateAsync();
        var runId = await SeedRunAsync(db);
        var repo = new ExtractedFactRepository(db);

        Assert.Empty(await repo.GetForRunTickerAsync(runId, Ticker.Create("NVDA")));
    }
}
