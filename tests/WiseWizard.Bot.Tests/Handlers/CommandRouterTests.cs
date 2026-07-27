using Microsoft.Extensions.Options;
using NSubstitute;
using WiseWizard.Bot;
using WiseWizard.Bot.Auth;
using WiseWizard.Bot.Handlers;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;
using WiseWizard.Core.Services;

namespace WiseWizard.Bot.Tests.Handlers;

public class CommandRouterTests
{
    private const long Owner = 42;
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    private sealed class Fixture
    {
        public IPositionsRepository Positions { get; } = Substitute.For<IPositionsRepository>();
        public IBrokerSessionRepository Sessions { get; } = Substitute.For<IBrokerSessionRepository>();
        public IRunRepository Runs { get; } = Substitute.For<IRunRepository>();
        public IVerdictRepository Verdicts { get; } = Substitute.For<IVerdictRepository>();
        public IWatchlistRepository Watchlist { get; } = Substitute.For<IWatchlistRepository>();
        public RecordingGateway Gateway { get; } = new();
        public CommandRouter Router { get; }

        public Fixture()
        {
            var clock = Substitute.For<IClock>();
            clock.UtcNow.Returns(Now);
            Positions.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns([]);
            Positions.GetTickersAsync(Arg.Any<CancellationToken>()).Returns([]);
            Sessions.GetAsync(Arg.Any<CancellationToken>())
                .Returns(new BrokerSessionState { Status = SessionStatus.Live });
            Runs.GetLatestFinishedAsync(Arg.Any<CancellationToken>()).Returns((Run?)null);
            Watchlist.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);
            Watchlist.ContainsAsync(Arg.Any<Ticker>(), Arg.Any<CancellationToken>()).Returns(false);
            Watchlist.CountAsync(Arg.Any<CancellationToken>()).Returns(0);
            Watchlist.AddAsync(Arg.Any<WatchlistEntry>(), Arg.Any<CancellationToken>()).Returns(true);
            Watchlist.RemoveAsync(Arg.Any<Ticker>(), Arg.Any<CancellationToken>()).Returns(true);

            var authorizer = new OwnerAuthorizer(
                Options.Create(new BotOptions { OwnerChatId = Owner, BotToken = "t" }));
            var service = new WatchlistService(Watchlist, Positions, clock);

            Router = new CommandRouter(
                authorizer,
                new PortfolioHandler(Positions, Sessions, Gateway, clock),
                new ReportHandler(Runs, Verdicts, Gateway),
                new WatchlistCommandHandler(service, Gateway),
                new DrillDownHandler(Runs, Verdicts, Gateway));
        }
    }

    [Fact]
    public async Task Portfolio_command_reads_positions()
    {
        var f = new Fixture();
        await f.Router.RouteMessageAsync(new IncomingMessage(Owner, "/portfolio"));
        await f.Positions.Received().GetCurrentAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Report_command_reads_latest_finished_run()
    {
        var f = new Fixture();
        await f.Router.RouteMessageAsync(new IncomingMessage(Owner, "/report"));
        await f.Runs.Received().GetLatestFinishedAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Watch_command_adds_to_watchlist()
    {
        var f = new Fixture();
        await f.Router.RouteMessageAsync(new IncomingMessage(Owner, "/watch AAPL"));
        await f.Watchlist.Received().AddAsync(Arg.Any<WatchlistEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unwatch_command_removes_from_watchlist()
    {
        var f = new Fixture();
        await f.Router.RouteMessageAsync(new IncomingMessage(Owner, "/unwatch AAPL"));
        await f.Watchlist.Received().RemoveAsync(Ticker.Create("AAPL"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Watchlist_command_lists()
    {
        var f = new Fixture();
        await f.Router.RouteMessageAsync(new IncomingMessage(Owner, "/watchlist"));
        await f.Watchlist.Received().GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Command_with_bot_mention_is_routed()
    {
        var f = new Fixture();
        await f.Router.RouteMessageAsync(new IncomingMessage(Owner, "/report@wisewizardbot"));
        await f.Runs.Received().GetLatestFinishedAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unknown_command_is_ignored()
    {
        var f = new Fixture();
        await f.Router.RouteMessageAsync(new IncomingMessage(Owner, "/nonsense"));
        Assert.Empty(f.Gateway.Sent);
    }

    [Fact]
    public async Task Empty_message_is_ignored()
    {
        var f = new Fixture();
        await f.Router.RouteMessageAsync(new IncomingMessage(Owner, "   "));
        Assert.Empty(f.Gateway.Sent);
    }

    [Fact]
    public async Task Non_owner_message_is_dropped_without_data_access()
    {
        var f = new Fixture();
        await f.Router.RouteMessageAsync(new IncomingMessage(999, "/portfolio"));
        Assert.Empty(f.Gateway.Sent);
        await f.Positions.DidNotReceive().GetCurrentAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Detail_callback_routes_to_drilldown()
    {
        var f = new Fixture();
        f.Runs.GetLatestFinishedAsync(Arg.Any<CancellationToken>())
            .Returns(new Run { RunId = 7, Status = RunStatus.Finished, StartedAt = Now, FinishedAt = Now });
        f.Verdicts.GetAsync(7, Ticker.Create("AAPL"), Arg.Any<CancellationToken>()).Returns((Verdict?)null);

        await f.Router.RouteCallbackAsync(new IncomingCallback(Owner, "cb1", "detail:AAPL"));

        Assert.Single(f.Gateway.Acked);
        Assert.Single(f.Gateway.Sent);
    }

    [Fact]
    public async Task Non_owner_callback_is_dropped()
    {
        var f = new Fixture();
        await f.Router.RouteCallbackAsync(new IncomingCallback(999, "cb1", "detail:AAPL"));
        Assert.Empty(f.Gateway.Sent);
        Assert.Empty(f.Gateway.Acked);
    }

    [Fact]
    public async Task Callback_with_unknown_prefix_is_ignored()
    {
        var f = new Fixture();
        await f.Router.RouteCallbackAsync(new IncomingCallback(Owner, "cb1", "other:AAPL"));
        Assert.Empty(f.Gateway.Sent);
        Assert.Empty(f.Gateway.Acked);
    }

    [Fact]
    public async Task Callback_with_invalid_ticker_is_ignored()
    {
        var f = new Fixture();
        await f.Router.RouteCallbackAsync(new IncomingCallback(Owner, "cb1", "detail:!!bad!!"));
        Assert.Empty(f.Gateway.Sent);
        Assert.Empty(f.Gateway.Acked);
    }
}
