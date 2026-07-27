using NSubstitute;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;
using WiseWizard.Core.Services;

namespace WiseWizard.Core.Tests;

public sealed class PortfolioRefreshServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 3, 0, 0, TimeSpan.Zero);

    private readonly IBrokerReader _broker = Substitute.For<IBrokerReader>();
    private readonly IPositionsRepository _positions = Substitute.For<IPositionsRepository>();
    private readonly IBrokerSessionRepository _sessions = Substitute.For<IBrokerSessionRepository>();
    private readonly IOwnerNotifier _notifier = Substitute.For<IOwnerNotifier>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public PortfolioRefreshServiceTests()
    {
        _clock.UtcNow.Returns(Now);
        _sessions.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new BrokerSessionState { Status = SessionStatus.Unknown });
    }

    private PortfolioRefreshService CreateSut() =>
        new(_broker, _positions, _sessions, _notifier, _clock);

    private static Position SamplePosition(string ticker = "AAPL") => new()
    {
        Ticker = Ticker.Create(ticker),
        Quantity = 10m,
        AvgCost = 150m,
        MarketValue = 2000m,
        UnrealizedPnl = 500m,
        Currency = "USD",
        AsOf = Now,
    };

    [Fact]
    public async Task RefreshAsync_HappyPath_ReplacesSnapshotAndMarksLive()
    {
        var snapshot = new[] { SamplePosition() };
        _broker.IsSessionLiveAsync(Arg.Any<CancellationToken>()).Returns(true);
        _broker.ReadPositionsAsync(Arg.Any<CancellationToken>()).Returns(snapshot);

        await CreateSut().RefreshAsync();

        await _positions.Received(1).ReplaceSnapshotAsync(
            Arg.Is<IReadOnlyList<Position>>(p => p!.Count == 1),
            Arg.Any<CancellationToken>());
        await _sessions.Received(1).SaveAsync(
            Arg.Is<BrokerSessionState>(s => s != null &&
                s.Status == SessionStatus.Live &&
                s.LastSnapshotAt == Now &&
                s.LastRefreshAttemptAt == Now &&
                s.LastRefreshOk == true &&
                s.ReauthAlertedAt == null),
            Arg.Any<CancellationToken>());
        await _notifier.DidNotReceive().NotifyAsync(
            Arg.Any<AlertKind>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_EmptyPortfolio_RecordsEmptyButCurrentSnapshot()
    {
        _broker.IsSessionLiveAsync(Arg.Any<CancellationToken>()).Returns(true);
        _broker.ReadPositionsAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Position>());

        await CreateSut().RefreshAsync();

        await _positions.Received(1).ReplaceSnapshotAsync(
            Arg.Is<IReadOnlyList<Position>>(p => p!.Count == 0),
            Arg.Any<CancellationToken>());
        await _sessions.Received(1).SaveAsync(
            Arg.Is<BrokerSessionState>(s => s != null &&
                s.Status == SessionStatus.Live &&
                s.LastSnapshotAt == Now &&
                s.LastRefreshOk == true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_ReaderThrowsMidRead_RetainsLastGoodAndRecordsFailure()
    {
        _sessions.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new BrokerSessionState
            {
                Status = SessionStatus.Live,
                LastSnapshotAt = Now.AddHours(-2),
            });
        _broker.IsSessionLiveAsync(Arg.Any<CancellationToken>()).Returns(true);
        _broker.ReadPositionsAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<Position>>(_ => throw new InvalidOperationException("gateway down"));

        await CreateSut().RefreshAsync();

        await _positions.DidNotReceive().ReplaceSnapshotAsync(
            Arg.Any<IReadOnlyList<Position>>(), Arg.Any<CancellationToken>());
        await _sessions.Received(1).SaveAsync(
            Arg.Is<BrokerSessionState>(s => s != null &&
                s.LastRefreshAttemptAt == Now &&
                s.LastRefreshOk == false &&
                s.LastSnapshotAt == Now.AddHours(-2)),
            Arg.Any<CancellationToken>());
        await _notifier.DidNotReceive().NotifyAsync(
            Arg.Any<AlertKind>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_SessionNotLive_MarksLapsedAndAlertsOnce()
    {
        _broker.IsSessionLiveAsync(Arg.Any<CancellationToken>()).Returns(false);

        await CreateSut().RefreshAsync();

        await _positions.DidNotReceive().ReplaceSnapshotAsync(
            Arg.Any<IReadOnlyList<Position>>(), Arg.Any<CancellationToken>());
        await _notifier.Received(1).NotifyAsync(
            AlertKind.BrokerReauthRequired, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _sessions.Received(1).SaveAsync(
            Arg.Is<BrokerSessionState>(s => s != null &&
                s.Status == SessionStatus.Lapsed &&
                s.LastRefreshOk == false &&
                s.ReauthAlertedAt == Now),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_IsSessionLiveThrows_TreatedAsNotLive()
    {
        _broker.IsSessionLiveAsync(Arg.Any<CancellationToken>())
            .Returns<bool>(_ => throw new HttpRequestException("unreachable"));

        await CreateSut().RefreshAsync();

        await _notifier.Received(1).NotifyAsync(
            AlertKind.BrokerReauthRequired, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _sessions.Received(1).SaveAsync(
            Arg.Is<BrokerSessionState>(s => s != null && s.Status == SessionStatus.Lapsed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_SecondLapse_DoesNotReAlert()
    {
        _sessions.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new BrokerSessionState
            {
                Status = SessionStatus.Lapsed,
                ReauthAlertedAt = Now.AddMinutes(-10),
            });
        _broker.IsSessionLiveAsync(Arg.Any<CancellationToken>()).Returns(false);

        await CreateSut().RefreshAsync();

        await _notifier.DidNotReceive().NotifyAsync(
            Arg.Any<AlertKind>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _sessions.Received(1).SaveAsync(
            Arg.Is<BrokerSessionState>(s => s != null &&
                s.Status == SessionStatus.Lapsed &&
                s.ReauthAlertedAt == Now.AddMinutes(-10)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_RecoveryAfterLapse_ClearsAlert()
    {
        _sessions.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new BrokerSessionState
            {
                Status = SessionStatus.Lapsed,
                ReauthAlertedAt = Now.AddMinutes(-30),
            });
        _broker.IsSessionLiveAsync(Arg.Any<CancellationToken>()).Returns(true);
        _broker.ReadPositionsAsync(Arg.Any<CancellationToken>()).Returns(new[] { SamplePosition() });

        await CreateSut().RefreshAsync();

        await _sessions.Received(1).SaveAsync(
            Arg.Is<BrokerSessionState>(s => s != null &&
                s.Status == SessionStatus.Live &&
                s.ReauthAlertedAt == null),
            Arg.Any<CancellationToken>());
    }
}
