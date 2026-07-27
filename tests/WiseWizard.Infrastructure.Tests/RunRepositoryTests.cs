using WiseWizard.Core.Models;
using WiseWizard.Infrastructure.Persistence;

namespace WiseWizard.Infrastructure.Tests;

public class RunRepositoryTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 26, 23, 0, 0, TimeSpan.Zero);

    private static Run NewRun(RunStatus status = RunStatus.Pending)
        => new() { Status = status, StartedAt = Start };

    [Fact]
    public async Task Create_returns_assigned_id()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new RunRepository(db);

        var created = await repo.CreateAsync(NewRun());

        Assert.True(created.RunId > 0);
    }

    [Fact]
    public async Task Update_round_trips_full_state_including_batch_ids_json()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new RunRepository(db);
        var created = await repo.CreateAsync(NewRun());

        var updated = created with
        {
            Status = RunStatus.Synthesizing,
            FinishedAt = null,
            BatchIds = new Dictionary<string, string> { ["cheap"] = "c1", ["synthesis"] = "s1" },
            CostCheapUsd = 0.10m,
            CostSynthesisUsd = 0.50m,
            CostTotalUsd = 0.60m,
            TokensCheap = 1000,
            TokensTotal = 1200,
            FailureReason = null,
        };
        await repo.UpdateAsync(updated);

        var loaded = await repo.GetAsync(created.RunId);

        Assert.NotNull(loaded);
        Assert.Equal(RunStatus.Synthesizing, loaded!.Status);
        Assert.Equal(Start, loaded.StartedAt);
        Assert.Equal("c1", loaded.BatchIds["cheap"]);
        Assert.Equal("s1", loaded.BatchIds["synthesis"]);
        Assert.Equal(0.10m, loaded.CostCheapUsd);
        Assert.Equal(0.50m, loaded.CostSynthesisUsd);
        Assert.Equal(0.60m, loaded.CostTotalUsd);
        Assert.Equal(1000, loaded.TokensCheap);
        Assert.Equal(1200, loaded.TokensTotal);
    }

    [Fact]
    public async Task Update_persists_finished_at_and_failure_reason()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new RunRepository(db);
        var created = await repo.CreateAsync(NewRun());

        var failed = created with
        {
            Status = RunStatus.Failed,
            FinishedAt = Start + TimeSpan.FromHours(1),
            FailureReason = "Cost ceiling reached.",
        };
        await repo.UpdateAsync(failed);

        var loaded = await repo.GetAsync(created.RunId);

        Assert.Equal(RunStatus.Failed, loaded!.Status);
        Assert.Equal(Start + TimeSpan.FromHours(1), loaded.FinishedAt);
        Assert.Equal("Cost ceiling reached.", loaded.FailureReason);
    }

    [Fact]
    public async Task Get_returns_null_when_missing()
    {
        await using var db = await TestDatabase.CreateAsync();
        Assert.Null(await new RunRepository(db).GetAsync(12345));
    }

    [Fact]
    public async Task Create_defaults_empty_batch_ids_to_empty_map()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new RunRepository(db);
        var created = await repo.CreateAsync(NewRun());

        var loaded = await repo.GetAsync(created.RunId);
        Assert.Empty(loaded!.BatchIds);
    }

    [Fact]
    public async Task GetLatestFinished_returns_most_recent_finished()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new RunRepository(db);

        var older = await repo.CreateAsync(NewRun());
        await repo.UpdateAsync(older with { Status = RunStatus.Finished, FinishedAt = Start });

        var newer = await repo.CreateAsync(NewRun());
        await repo.UpdateAsync(newer with { Status = RunStatus.Finished, FinishedAt = Start + TimeSpan.FromDays(1) });

        var pending = await repo.CreateAsync(NewRun(RunStatus.Extracting));
        await repo.UpdateAsync(pending with { Status = RunStatus.Extracting });

        var latest = await repo.GetLatestFinishedAsync();

        Assert.Equal(newer.RunId, latest!.RunId);
    }

    [Fact]
    public async Task GetLatestFinished_null_when_none_finished()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new RunRepository(db);
        await repo.CreateAsync(NewRun(RunStatus.Extracting));

        Assert.Null(await repo.GetLatestFinishedAsync());
    }

    [Fact]
    public async Task GetResumable_excludes_finished_and_failed()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new RunRepository(db);

        var finished = await repo.CreateAsync(NewRun());
        await repo.UpdateAsync(finished with { Status = RunStatus.Finished, FinishedAt = Start });

        var failed = await repo.CreateAsync(NewRun());
        await repo.UpdateAsync(failed with { Status = RunStatus.Failed, FinishedAt = Start });

        var extracting = await repo.CreateAsync(NewRun());
        await repo.UpdateAsync(extracting with { Status = RunStatus.Extracting });

        var synth = await repo.CreateAsync(NewRun());
        await repo.UpdateAsync(synth with { Status = RunStatus.Synthesizing });

        var resumable = await repo.GetResumableAsync();

        Assert.Equal(2, resumable.Count);
        Assert.Contains(resumable, r => r.RunId == extracting.RunId);
        Assert.Contains(resumable, r => r.RunId == synth.RunId);
    }
}
