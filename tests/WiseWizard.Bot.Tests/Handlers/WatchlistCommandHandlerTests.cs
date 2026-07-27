using NSubstitute;
using WiseWizard.Bot.Handlers;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;
using WiseWizard.Core.Services;

namespace WiseWizard.Bot.Tests.Handlers;

public class WatchlistCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    private sealed class Fixture
    {
        public IWatchlistRepository Watchlist { get; } = Substitute.For<IWatchlistRepository>();
        public IPositionsRepository Positions { get; } = Substitute.For<IPositionsRepository>();
        public RecordingGateway Gateway { get; } = new();
        public WatchlistCommandHandler Handler { get; }

        public Fixture()
        {
            var clock = Substitute.For<IClock>();
            clock.UtcNow.Returns(Now);
            // Sensible defaults: empty owned + empty watchlist.
            Positions.GetTickersAsync(Arg.Any<CancellationToken>()).Returns([]);
            Watchlist.ContainsAsync(Arg.Any<Ticker>(), Arg.Any<CancellationToken>()).Returns(false);
            Watchlist.CountAsync(Arg.Any<CancellationToken>()).Returns(0);
            Watchlist.AddAsync(Arg.Any<WatchlistEntry>(), Arg.Any<CancellationToken>()).Returns(true);
            var service = new WatchlistService(Watchlist, Positions, clock);
            Handler = new WatchlistCommandHandler(service, Gateway);
        }

        public string LastText => Gateway.Sent[^1].Text;
    }

    [Fact]
    public async Task Watch_adds_symbol()
    {
        var f = new Fixture();
        await f.Handler.HandleWatchAsync(42, "aapl");
        Assert.Contains("Added AAPL", f.LastText);
    }

    [Fact]
    public async Task Watch_with_note_added()
    {
        var f = new Fixture();
        await f.Handler.HandleWatchAsync(42, "aapl watching earnings");
        Assert.Contains("Added AAPL", f.LastText);
        await f.Watchlist.Received().AddAsync(
            Arg.Is<WatchlistEntry>(e => e != null && e.Note == "watching earnings"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Watch_empty_arguments_shows_usage()
    {
        var f = new Fixture();
        await f.Handler.HandleWatchAsync(42, "   ");
        Assert.Contains("Usage", f.LastText);
    }

    [Fact]
    public async Task Watch_invalid_symbol()
    {
        var f = new Fixture();
        await f.Handler.HandleWatchAsync(42, "@#$%^ rest");
        Assert.Contains("not a valid ticker", f.LastText);
    }

    [Fact]
    public async Task Watch_duplicate()
    {
        var f = new Fixture();
        f.Watchlist.ContainsAsync(Ticker.Create("AAPL"), Arg.Any<CancellationToken>()).Returns(true);
        await f.Handler.HandleWatchAsync(42, "aapl");
        Assert.Contains("already on your watchlist", f.LastText);
    }

    [Fact]
    public async Task Watch_already_owned()
    {
        var f = new Fixture();
        f.Positions.GetTickersAsync(Arg.Any<CancellationToken>()).Returns([Ticker.Create("AAPL")]);
        await f.Handler.HandleWatchAsync(42, "aapl");
        Assert.Contains("owned position", f.LastText);
    }

    [Fact]
    public async Task Watch_full()
    {
        var f = new Fixture();
        f.Watchlist.CountAsync(Arg.Any<CancellationToken>()).Returns(WatchlistService.MaxSize);
        await f.Handler.HandleWatchAsync(42, "aapl");
        Assert.Contains("full", f.LastText);
    }

    [Fact]
    public async Task Watch_note_too_long()
    {
        var f = new Fixture();
        var longNote = new string('x', WatchlistService.MaxNoteLength + 1);
        await f.Handler.HandleWatchAsync(42, $"aapl {longNote}");
        Assert.Contains("too long", f.LastText);
    }

    [Fact]
    public async Task Watch_race_returns_already_on_watchlist()
    {
        var f = new Fixture();
        f.Watchlist.AddAsync(Arg.Any<WatchlistEntry>(), Arg.Any<CancellationToken>()).Returns(false);
        await f.Handler.HandleWatchAsync(42, "aapl");
        Assert.Contains("already on your watchlist", f.LastText);
    }

    [Fact]
    public async Task Unwatch_removes()
    {
        var f = new Fixture();
        f.Watchlist.RemoveAsync(Ticker.Create("AAPL"), Arg.Any<CancellationToken>()).Returns(true);
        await f.Handler.HandleUnwatchAsync(42, "aapl");
        Assert.Contains("Removed AAPL", f.LastText);
    }

    [Fact]
    public async Task Unwatch_not_found()
    {
        var f = new Fixture();
        f.Watchlist.RemoveAsync(Arg.Any<Ticker>(), Arg.Any<CancellationToken>()).Returns(false);
        await f.Handler.HandleUnwatchAsync(42, "aapl");
        Assert.Contains("was not on your watchlist", f.LastText);
    }

    [Fact]
    public async Task Unwatch_empty_shows_usage()
    {
        var f = new Fixture();
        await f.Handler.HandleUnwatchAsync(42, "");
        Assert.Contains("Usage", f.LastText);
    }

    [Fact]
    public async Task List_empty()
    {
        var f = new Fixture();
        f.Watchlist.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);
        await f.Handler.HandleListAsync(42);
        Assert.Contains("empty", f.LastText);
    }

    [Fact]
    public async Task List_shows_entries_with_and_without_notes()
    {
        var f = new Fixture();
        f.Watchlist.GetAllAsync(Arg.Any<CancellationToken>()).Returns([
            new WatchlistEntry { Ticker = Ticker.Create("AAPL"), AddedAt = Now, Note = "earnings" },
            new WatchlistEntry { Ticker = Ticker.Create("MSFT"), AddedAt = Now },
        ]);
        await f.Handler.HandleListAsync(42);
        Assert.Contains("AAPL", f.LastText);
        Assert.Contains("earnings", f.LastText);
        Assert.Contains("MSFT", f.LastText);
    }
}
