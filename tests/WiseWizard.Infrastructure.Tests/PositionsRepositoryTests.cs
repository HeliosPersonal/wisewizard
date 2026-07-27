using WiseWizard.Core.Models;
using WiseWizard.Infrastructure.Persistence;

namespace WiseWizard.Infrastructure.Tests;

public sealed class PositionsRepositoryTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 7, 26, 3, 0, 0, TimeSpan.Zero);

    private static Position MakePosition(
        string ticker,
        decimal quantity = 10m,
        decimal avgCost = 100m,
        decimal marketValue = 1200m,
        decimal unrealizedPnl = 200m,
        string currency = "USD",
        DateTimeOffset? asOf = null) => new()
    {
        Ticker = Ticker.Create(ticker),
        Quantity = quantity,
        AvgCost = avgCost,
        MarketValue = marketValue,
        UnrealizedPnl = unrealizedPnl,
        Currency = currency,
        AsOf = asOf ?? AsOf,
    };

    [Fact]
    public async Task ReplaceSnapshot_ThenGetCurrent_RoundtripsAllFields()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new PositionsRepository(db);

        var original = MakePosition("AAPL", 12.5m, 149.99m, 2100.75m, 350.25m, "USD");
        await repo.ReplaceSnapshotAsync(new[] { original });

        var current = await repo.GetCurrentAsync();

        var loaded = Assert.Single(current);
        Assert.Equal(Ticker.Create("AAPL"), loaded.Ticker);
        Assert.Equal(12.5m, loaded.Quantity);
        Assert.Equal(149.99m, loaded.AvgCost);
        Assert.Equal(2100.75m, loaded.MarketValue);
        Assert.Equal(350.25m, loaded.UnrealizedPnl);
        Assert.Equal("USD", loaded.Currency);
        Assert.Equal(AsOf, loaded.AsOf);
    }

    [Fact]
    public async Task ReplaceSnapshot_OverwritesWholesale_NoStaleRows()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new PositionsRepository(db);

        await repo.ReplaceSnapshotAsync(new[]
        {
            MakePosition("AAPL"),
            MakePosition("MSFT"),
            MakePosition("GOOG"),
        });

        await repo.ReplaceSnapshotAsync(new[] { MakePosition("TSLA") });

        var current = await repo.GetCurrentAsync();
        var loaded = Assert.Single(current);
        Assert.Equal(Ticker.Create("TSLA"), loaded.Ticker);
    }

    [Fact]
    public async Task ReplaceSnapshot_Empty_ClearsAllRows()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new PositionsRepository(db);

        await repo.ReplaceSnapshotAsync(new[] { MakePosition("AAPL") });
        await repo.ReplaceSnapshotAsync(Array.Empty<Position>());

        var current = await repo.GetCurrentAsync();
        Assert.Empty(current);
    }

    [Fact]
    public async Task GetCurrent_WhenEmpty_ReturnsEmpty()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new PositionsRepository(db);

        var current = await repo.GetCurrentAsync();

        Assert.Empty(current);
    }

    [Fact]
    public async Task GetTickers_ReturnsDistinctSortedTickers()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new PositionsRepository(db);

        await repo.ReplaceSnapshotAsync(new[]
        {
            MakePosition("MSFT"),
            MakePosition("AAPL"),
        });

        var tickers = await repo.GetTickersAsync();

        Assert.Equal(new[] { Ticker.Create("AAPL"), Ticker.Create("MSFT") }, tickers);
    }

    [Fact]
    public async Task GetTickers_WhenEmpty_ReturnsEmpty()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new PositionsRepository(db);

        var tickers = await repo.GetTickersAsync();

        Assert.Empty(tickers);
    }

    [Fact]
    public async Task ReplaceSnapshot_PreservesNonUsdCurrency()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new PositionsRepository(db);

        await repo.ReplaceSnapshotAsync(new[] { MakePosition("SHOP", currency: "CAD") });

        var loaded = Assert.Single(await repo.GetCurrentAsync());
        Assert.Equal("CAD", loaded.Currency);
    }

    [Fact]
    public async Task ReplaceSnapshot_NegativeQuantityAndPnl_Roundtrip()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new PositionsRepository(db);

        await repo.ReplaceSnapshotAsync(new[]
        {
            MakePosition("SPY", quantity: -5m, unrealizedPnl: -123.45m),
        });

        var loaded = Assert.Single(await repo.GetCurrentAsync());
        Assert.Equal(-5m, loaded.Quantity);
        Assert.Equal(-123.45m, loaded.UnrealizedPnl);
    }

    [Fact]
    public async Task ReplaceSnapshot_Null_Throws()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new PositionsRepository(db);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => repo.ReplaceSnapshotAsync(null!));
    }

    [Fact]
    public async Task ReplaceSnapshot_SharedAsOf_AppliedToEveryRow()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new PositionsRepository(db);

        await repo.ReplaceSnapshotAsync(new[]
        {
            MakePosition("AAPL"),
            MakePosition("MSFT"),
        });

        var current = await repo.GetCurrentAsync();
        Assert.All(current, p => Assert.Equal(AsOf, p.AsOf));
    }
}
