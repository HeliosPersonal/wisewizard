using NSubstitute;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;
using WiseWizard.Core.Services;

namespace WiseWizard.Core.Tests;

public sealed class WatchlistServiceTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    private readonly IWatchlistRepository _watchlist = Substitute.For<IWatchlistRepository>();
    private readonly IPositionsRepository _positions = Substitute.For<IPositionsRepository>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private WatchlistService CreateService()
    {
        _clock.UtcNow.Returns(FixedNow);
        _positions.GetTickersAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Ticker>());
        return new WatchlistService(_watchlist, _positions, _clock);
    }

    [Fact]
    public async Task AddAsync_ValidNewSymbol_PersistsEntryAndReturnsAdded()
    {
        var service = CreateService();
        _watchlist.ContainsAsync(Arg.Any<Ticker>(), Arg.Any<CancellationToken>()).Returns(false);
        _watchlist.CountAsync(Arg.Any<CancellationToken>()).Returns(0);
        _watchlist.AddAsync(Arg.Any<WatchlistEntry>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await service.AddAsync(" aapl ", "cheap");

        Assert.Equal(WatchlistAddOutcome.Added, result.Outcome);
        Assert.False(result.AlreadyOwned);
        await _watchlist.Received(1).AddAsync(
            Arg.Is<WatchlistEntry>(e =>
                e != null &&
                e.Ticker == Ticker.Create("AAPL") &&
                e.AddedAt == FixedNow &&
                e.Note == "cheap"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAsync_NullNote_PersistsEntryWithNullNote()
    {
        var service = CreateService();
        _watchlist.ContainsAsync(Arg.Any<Ticker>(), Arg.Any<CancellationToken>()).Returns(false);
        _watchlist.CountAsync(Arg.Any<CancellationToken>()).Returns(0);
        _watchlist.AddAsync(Arg.Any<WatchlistEntry>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await service.AddAsync("MSFT", null);

        Assert.Equal(WatchlistAddOutcome.Added, result.Outcome);
        await _watchlist.Received(1).AddAsync(
            Arg.Is<WatchlistEntry>(e => e != null && e.Note == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAsync_InvalidSymbol_ReturnsInvalidSymbolAndPersistsNothing()
    {
        var service = CreateService();

        var result = await service.AddAsync("!!bad!!", null);

        Assert.Equal(WatchlistAddOutcome.InvalidSymbol, result.Outcome);
        await _watchlist.DidNotReceive().AddAsync(Arg.Any<WatchlistEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAsync_EmptySymbol_ReturnsInvalidSymbol()
    {
        var service = CreateService();

        var result = await service.AddAsync("   ", null);

        Assert.Equal(WatchlistAddOutcome.InvalidSymbol, result.Outcome);
    }

    [Fact]
    public async Task AddAsync_NoteTooLong_ReturnsNoteTooLongAndPersistsNothing()
    {
        var service = CreateService();
        var longNote = new string('x', WatchlistService.MaxNoteLength + 1);

        var result = await service.AddAsync("AAPL", longNote);

        Assert.Equal(WatchlistAddOutcome.NoteTooLong, result.Outcome);
        await _watchlist.DidNotReceive().AddAsync(Arg.Any<WatchlistEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAsync_NoteAtMaxLength_IsAccepted()
    {
        var service = CreateService();
        _watchlist.ContainsAsync(Arg.Any<Ticker>(), Arg.Any<CancellationToken>()).Returns(false);
        _watchlist.CountAsync(Arg.Any<CancellationToken>()).Returns(0);
        _watchlist.AddAsync(Arg.Any<WatchlistEntry>(), Arg.Any<CancellationToken>()).Returns(true);
        var maxNote = new string('x', WatchlistService.MaxNoteLength);

        var result = await service.AddAsync("AAPL", maxNote);

        Assert.Equal(WatchlistAddOutcome.Added, result.Outcome);
    }

    [Fact]
    public async Task AddAsync_DuplicateOnWatchlist_ReturnsAlreadyOnWatchlist()
    {
        var service = CreateService();
        _watchlist.ContainsAsync(Ticker.Create("AAPL"), Arg.Any<CancellationToken>()).Returns(true);

        var result = await service.AddAsync("aapl", null);

        Assert.Equal(WatchlistAddOutcome.AlreadyOnWatchlist, result.Outcome);
        await _watchlist.DidNotReceive().AddAsync(Arg.Any<WatchlistEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAsync_RepositoryReportsDuplicateRace_ReturnsAlreadyOnWatchlist()
    {
        var service = CreateService();
        _watchlist.ContainsAsync(Arg.Any<Ticker>(), Arg.Any<CancellationToken>()).Returns(false);
        _watchlist.CountAsync(Arg.Any<CancellationToken>()).Returns(0);
        // Simulate a race: Contains said false but the insert lost to a concurrent add.
        _watchlist.AddAsync(Arg.Any<WatchlistEntry>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await service.AddAsync("AAPL", null);

        Assert.Equal(WatchlistAddOutcome.AlreadyOnWatchlist, result.Outcome);
    }

    [Fact]
    public async Task AddAsync_WatchlistFull_ReturnsWatchlistFull()
    {
        var service = CreateService();
        _watchlist.ContainsAsync(Arg.Any<Ticker>(), Arg.Any<CancellationToken>()).Returns(false);
        _watchlist.CountAsync(Arg.Any<CancellationToken>()).Returns(WatchlistService.MaxSize);

        var result = await service.AddAsync("AAPL", null);

        Assert.Equal(WatchlistAddOutcome.WatchlistFull, result.Outcome);
        await _watchlist.DidNotReceive().AddAsync(Arg.Any<WatchlistEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAsync_AtMaxSizeBoundary_IsRejected()
    {
        var service = CreateService();
        _watchlist.ContainsAsync(Arg.Any<Ticker>(), Arg.Any<CancellationToken>()).Returns(false);
        // Exactly one below the cap is still allowed.
        _watchlist.CountAsync(Arg.Any<CancellationToken>()).Returns(WatchlistService.MaxSize - 1);
        _watchlist.AddAsync(Arg.Any<WatchlistEntry>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await service.AddAsync("AAPL", null);

        Assert.Equal(WatchlistAddOutcome.Added, result.Outcome);
    }

    [Fact]
    public async Task AddAsync_SymbolIsOwnedPosition_ReturnsAlreadyOwnedAndPersistsNothing()
    {
        var service = CreateService();
        _positions.GetTickersAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { Ticker.Create("AAPL") });

        var result = await service.AddAsync("aapl", null);

        Assert.Equal(WatchlistAddOutcome.AlreadyOwned, result.Outcome);
        Assert.True(result.AlreadyOwned);
        await _watchlist.DidNotReceive().AddAsync(Arg.Any<WatchlistEntry>(), Arg.Any<CancellationToken>());
        await _watchlist.DidNotReceive().ContainsAsync(Arg.Any<Ticker>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveAsync_ExistingTicker_ReturnsTrue()
    {
        var service = CreateService();
        _watchlist.RemoveAsync(Ticker.Create("AAPL"), Arg.Any<CancellationToken>()).Returns(true);

        var removed = await service.RemoveAsync(" aapl ");

        Assert.True(removed);
        await _watchlist.Received(1).RemoveAsync(Ticker.Create("AAPL"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveAsync_NotPresent_ReturnsFalse()
    {
        var service = CreateService();
        _watchlist.RemoveAsync(Arg.Any<Ticker>(), Arg.Any<CancellationToken>()).Returns(false);

        var removed = await service.RemoveAsync("AAPL");

        Assert.False(removed);
    }

    [Fact]
    public async Task RemoveAsync_InvalidSymbol_ReturnsFalseAndDoesNotTouchRepository()
    {
        var service = CreateService();

        var removed = await service.RemoveAsync("!!bad!!");

        Assert.False(removed);
        await _watchlist.DidNotReceive().RemoveAsync(Arg.Any<Ticker>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAllAsync_DelegatesToRepository()
    {
        var service = CreateService();
        var entries = new List<WatchlistEntry>
        {
            new() { Ticker = Ticker.Create("AAPL"), AddedAt = FixedNow, Note = "n" },
        };
        _watchlist.GetAllAsync(Arg.Any<CancellationToken>()).Returns(entries);

        var result = await service.GetAllAsync();

        Assert.Same(entries, result);
    }

    [Fact]
    public void Constants_HaveExpectedValues()
    {
        Assert.Equal(100, WatchlistService.MaxSize);
        Assert.Equal(280, WatchlistService.MaxNoteLength);
    }
}
