using NSubstitute;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;
using WiseWizard.Core.Services;

namespace WiseWizard.Core.Tests;

public class UniverseProviderTests
{
    private static WatchlistEntry Entry(string ticker)
        => new() { Ticker = Ticker.Create(ticker), AddedAt = DateTimeOffset.UnixEpoch };

    [Fact]
    public async Task Unions_and_deduplicates_positions_and_watchlist()
    {
        var positions = Substitute.For<IPositionsRepository>();
        positions.GetTickersAsync(Arg.Any<CancellationToken>())
            .Returns([Ticker.Create("AAPL"), Ticker.Create("MSFT")]);

        var watchlist = Substitute.For<IWatchlistRepository>();
        watchlist.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([Entry("MSFT"), Entry("NVDA")]);

        var provider = new UniverseProvider(positions, watchlist);

        var universe = await provider.GetUniverseAsync();

        Assert.Equal(
            new[] { "AAPL", "MSFT", "NVDA" },
            universe.Select(t => t.Value).ToArray());
    }

    [Fact]
    public async Task Empty_when_both_empty()
    {
        var positions = Substitute.For<IPositionsRepository>();
        positions.GetTickersAsync(Arg.Any<CancellationToken>()).Returns([]);
        var watchlist = Substitute.For<IWatchlistRepository>();
        watchlist.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);

        var universe = await new UniverseProvider(positions, watchlist).GetUniverseAsync();

        Assert.Empty(universe);
    }
}
