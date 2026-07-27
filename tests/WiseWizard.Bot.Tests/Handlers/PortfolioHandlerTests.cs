using NSubstitute;
using WiseWizard.Bot.Handlers;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;

namespace WiseWizard.Bot.Tests.Handlers;

public class PortfolioHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    private static Position P(string ticker) => new()
    {
        Ticker = Ticker.Create(ticker),
        Quantity = 10m,
        AvgCost = 10m,
        MarketValue = 2000m,
        UnrealizedPnl = 150m,
        AsOf = Now - TimeSpan.FromHours(2),
    };

    private static (PortfolioHandler Handler, RecordingGateway Gateway, IBrokerSessionRepository Sessions, IPositionsRepository Positions) Build()
    {
        var positions = Substitute.For<IPositionsRepository>();
        var sessions = Substitute.For<IBrokerSessionRepository>();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);
        var gateway = new RecordingGateway();
        return (new PortfolioHandler(positions, sessions, gateway, clock), gateway, sessions, positions);
    }

    [Fact]
    public async Task Sends_portfolio_summary_with_positions()
    {
        var (handler, gateway, sessions, positions) = Build();
        positions.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns([P("AAPL")]);
        sessions.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new BrokerSessionState { Status = SessionStatus.Live });

        await handler.HandleAsync(42);

        var sent = Assert.Single(gateway.Sent);
        Assert.Equal(42, sent.ChatId);
        Assert.Contains("AAPL", sent.Text);
        Assert.Null(sent.Buttons);
    }

    [Fact]
    public async Task Sends_empty_state_when_no_positions()
    {
        var (handler, gateway, sessions, positions) = Build();
        positions.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns([]);
        sessions.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new BrokerSessionState { Status = SessionStatus.Live });

        await handler.HandleAsync(42);

        var sent = Assert.Single(gateway.Sent);
        Assert.Contains("no open positions", sent.Text, StringComparison.OrdinalIgnoreCase);
    }
}
