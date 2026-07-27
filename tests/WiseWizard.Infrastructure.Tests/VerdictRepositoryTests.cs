using WiseWizard.Core.Models;
using WiseWizard.Infrastructure.Persistence;

namespace WiseWizard.Infrastructure.Tests;

public class VerdictRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    private static async Task<long> SeedRunAsync(TestDatabase db, DateTimeOffset? startedAt = null)
    {
        var run = await new RunRepository(db).CreateAsync(
            new Run { Status = RunStatus.Persisting, StartedAt = startedAt ?? Now });
        return run.RunId;
    }

    private static Verdict Verdict(
        long runId, string ticker, Signal signal, IReadOnlyList<string> sources,
        string summary = "summary", DateTimeOffset? createdAt = null)
        => new()
        {
            RunId = runId,
            Ticker = Ticker.Create(ticker),
            Signal = signal,
            SummaryLine = summary,
            FullReasoning = "reasoning",
            Sources = sources,
            ChangeFromYesterday = "delta",
            CreatedAt = createdAt ?? Now,
        };

    [Fact]
    public async Task Upsert_then_get_round_trips_including_sources_json_and_signal()
    {
        await using var db = await TestDatabase.CreateAsync();
        var runId = await SeedRunAsync(db);
        var repo = new VerdictRepository(db);

        await repo.UpsertAsync(Verdict(runId, "AAPL", Signal.Review, ["d1", "d2"]));

        var loaded = await repo.GetAsync(runId, Ticker.Create("AAPL"));

        Assert.NotNull(loaded);
        Assert.Equal(Signal.Review, loaded!.Signal);
        Assert.Equal(new[] { "d1", "d2" }, loaded.Sources);
        Assert.Equal("delta", loaded.ChangeFromYesterday);
        Assert.Equal(Now, loaded.CreatedAt);
        Assert.True(loaded.HasEvidence);
    }

    [Fact]
    public async Task Upsert_is_idempotent_on_run_and_ticker()
    {
        await using var db = await TestDatabase.CreateAsync();
        var runId = await SeedRunAsync(db);
        var repo = new VerdictRepository(db);

        await repo.UpsertAsync(Verdict(runId, "AAPL", Signal.Hold, ["d1"], summary: "first"));
        await repo.UpsertAsync(Verdict(runId, "AAPL", Signal.Review, ["d2"], summary: "second"));

        var all = await repo.GetForRunAsync(runId);
        var single = Assert.Single(all);
        Assert.Equal(Signal.Review, single.Signal);
        Assert.Equal("second", single.SummaryLine);
        Assert.Equal(new[] { "d2" }, single.Sources);
    }

    [Fact]
    public async Task GetForRun_returns_all_verdicts_of_a_run()
    {
        await using var db = await TestDatabase.CreateAsync();
        var runId = await SeedRunAsync(db);
        var repo = new VerdictRepository(db);

        await repo.UpsertAsync(Verdict(runId, "AAPL", Signal.Hold, ["d1"]));
        await repo.UpsertAsync(Verdict(runId, "MSFT", Signal.Attention, ["d2"]));

        var all = await repo.GetForRunAsync(runId);

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task GetAsync_null_when_missing()
    {
        await using var db = await TestDatabase.CreateAsync();
        var runId = await SeedRunAsync(db);
        Assert.Null(await new VerdictRepository(db).GetAsync(runId, Ticker.Create("AAPL")));
    }

    [Fact]
    public async Task GetPrevious_picks_most_recent_prior_run()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new VerdictRepository(db);

        var run1 = await SeedRunAsync(db, Now - TimeSpan.FromDays(2));
        var run2 = await SeedRunAsync(db, Now - TimeSpan.FromDays(1));
        var run3 = await SeedRunAsync(db, Now);

        await repo.UpsertAsync(Verdict(run1, "AAPL", Signal.Hold, ["d1"], summary: "two days ago",
            createdAt: Now - TimeSpan.FromDays(2)));
        await repo.UpsertAsync(Verdict(run2, "AAPL", Signal.Attention, ["d2"], summary: "yesterday",
            createdAt: Now - TimeSpan.FromDays(1)));

        var previous = await repo.GetPreviousAsync(Ticker.Create("AAPL"), run3);

        Assert.NotNull(previous);
        Assert.Equal(run2, previous!.RunId);
        Assert.Equal("yesterday", previous.SummaryLine);
    }

    [Fact]
    public async Task GetPrevious_null_when_no_prior_run()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new VerdictRepository(db);
        var run1 = await SeedRunAsync(db);

        await repo.UpsertAsync(Verdict(run1, "AAPL", Signal.Hold, ["d1"]));

        // Nothing strictly before run1.
        Assert.Null(await repo.GetPreviousAsync(Ticker.Create("AAPL"), run1));
    }

    [Fact]
    public async Task Empty_sources_round_trip_as_empty_list()
    {
        await using var db = await TestDatabase.CreateAsync();
        var runId = await SeedRunAsync(db);
        var repo = new VerdictRepository(db);

        await repo.UpsertAsync(Verdict(runId, "AAPL", Signal.Hold, []));

        var loaded = await repo.GetAsync(runId, Ticker.Create("AAPL"));

        Assert.NotNull(loaded);
        Assert.Empty(loaded!.Sources);
        Assert.False(loaded.HasEvidence);
    }
}
