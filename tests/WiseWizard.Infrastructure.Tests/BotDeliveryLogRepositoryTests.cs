using WiseWizard.Infrastructure.Persistence;

namespace WiseWizard.Infrastructure.Tests;

public class BotDeliveryLogRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task First_delivery_returns_true()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new BotDeliveryLogRepository(db);

        var first = await repo.TryMarkDeliveredAsync("run_failed:7", 7, Now);

        Assert.True(first);
    }

    [Fact]
    public async Task Duplicate_event_key_returns_false()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new BotDeliveryLogRepository(db);

        await repo.TryMarkDeliveredAsync("run_failed:7", 7, Now);
        var second = await repo.TryMarkDeliveredAsync("run_failed:7", 7, Now + TimeSpan.FromMinutes(5));

        Assert.False(second);
    }

    [Fact]
    public async Task Distinct_event_keys_each_return_true()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new BotDeliveryLogRepository(db);

        var a = await repo.TryMarkDeliveredAsync("run_failed:7", 7, Now);
        var b = await repo.TryMarkDeliveredAsync("session_lapse:2026-07-26T00:00:00Z", null, Now);

        Assert.True(a);
        Assert.True(b);
    }

    [Fact]
    public async Task Null_run_id_is_accepted_for_session_events()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new BotDeliveryLogRepository(db);

        var ok = await repo.TryMarkDeliveredAsync("session_lapse:T", null, Now);

        Assert.True(ok);
    }

    [Fact]
    public async Task Blank_event_key_is_rejected()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new BotDeliveryLogRepository(db);

        await Assert.ThrowsAsync<ArgumentException>(
            () => repo.TryMarkDeliveredAsync("  ", null, Now));
    }
}
