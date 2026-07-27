using WiseWizard.Core.Models;
using WiseWizard.Infrastructure.Persistence;

namespace WiseWizard.Infrastructure.Tests;

public sealed class WatchlistRepositoryTests
{
    private static readonly DateTimeOffset BaseTime =
        new(2026, 7, 26, 8, 0, 0, TimeSpan.Zero);

    private static WatchlistEntry Entry(string ticker, DateTimeOffset addedAt, string? note = null) =>
        new()
        {
            Ticker = Ticker.Create(ticker),
            AddedAt = addedAt,
            Note = note,
        };

    [Fact]
    public async Task AddAndGet_Roundtrip_PersistsTickerNoteAndTimestamp()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new WatchlistRepository(db);

        var added = await repo.AddAsync(Entry("AAPL", BaseTime, "cheap"));

        Assert.True(added);
        var all = await repo.GetAllAsync();
        var entry = Assert.Single(all);
        Assert.Equal("AAPL", entry.Ticker.Value);
        Assert.Equal("cheap", entry.Note);
        Assert.Equal(BaseTime, entry.AddedAt);
    }

    [Fact]
    public async Task AddAsync_NullNote_PersistsAsNull()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new WatchlistRepository(db);

        await repo.AddAsync(Entry("MSFT", BaseTime, note: null));

        var entry = Assert.Single(await repo.GetAllAsync());
        Assert.Null(entry.Note);
    }

    [Fact]
    public async Task AddAsync_DuplicateTicker_ReturnsFalseAndKeepsOneRow()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new WatchlistRepository(db);

        var first = await repo.AddAsync(Entry("AAPL", BaseTime, "first"));
        var second = await repo.AddAsync(Entry("AAPL", BaseTime.AddHours(1), "second"));

        Assert.True(first);
        Assert.False(second);
        var entry = Assert.Single(await repo.GetAllAsync());
        Assert.Equal("first", entry.Note);
    }

    [Fact]
    public async Task RemoveAsync_ExistingTicker_ReturnsTrueAndRemovesRow()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new WatchlistRepository(db);
        await repo.AddAsync(Entry("AAPL", BaseTime));

        var removed = await repo.RemoveAsync(Ticker.Create("AAPL"));

        Assert.True(removed);
        Assert.Empty(await repo.GetAllAsync());
    }

    [Fact]
    public async Task RemoveAsync_AbsentTicker_ReturnsFalse()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new WatchlistRepository(db);

        var removed = await repo.RemoveAsync(Ticker.Create("NONE"));

        Assert.False(removed);
    }

    [Fact]
    public async Task ContainsAsync_ReflectsPresence()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new WatchlistRepository(db);
        await repo.AddAsync(Entry("AAPL", BaseTime));

        Assert.True(await repo.ContainsAsync(Ticker.Create("AAPL")));
        Assert.False(await repo.ContainsAsync(Ticker.Create("MSFT")));
    }

    [Fact]
    public async Task CountAsync_ReturnsNumberOfEntries()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new WatchlistRepository(db);

        Assert.Equal(0, await repo.CountAsync());

        await repo.AddAsync(Entry("AAPL", BaseTime));
        await repo.AddAsync(Entry("MSFT", BaseTime.AddMinutes(1)));

        Assert.Equal(2, await repo.CountAsync());
    }

    [Fact]
    public async Task GetAllAsync_OrdersByAddedAt()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new WatchlistRepository(db);

        await repo.AddAsync(Entry("CCC", BaseTime.AddHours(2)));
        await repo.AddAsync(Entry("AAA", BaseTime));
        await repo.AddAsync(Entry("BBB", BaseTime.AddHours(1)));

        var all = await repo.GetAllAsync();

        Assert.Equal(new[] { "AAA", "BBB", "CCC" }, all.Select(e => e.Ticker.Value).ToArray());
    }

    [Fact]
    public async Task GetAllAsync_EmptyWatchlist_ReturnsEmptyList()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new WatchlistRepository(db);

        Assert.Empty(await repo.GetAllAsync());
    }

    [Fact]
    public async Task AddedAt_RoundtripsAsUtc_RegardlessOfSourceOffset()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new WatchlistRepository(db);
        // A non-UTC offset should be normalized to UTC on the way in and out.
        var withOffset = new DateTimeOffset(2026, 7, 26, 10, 0, 0, TimeSpan.FromHours(2));

        await repo.AddAsync(Entry("AAPL", withOffset));

        var entry = Assert.Single(await repo.GetAllAsync());
        Assert.Equal(TimeSpan.Zero, entry.AddedAt.Offset);
        Assert.Equal(withOffset.UtcDateTime, entry.AddedAt.UtcDateTime);
    }
}
