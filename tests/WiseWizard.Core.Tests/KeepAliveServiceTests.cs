using NSubstitute;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;
using WiseWizard.Core.Services;

namespace WiseWizard.Core.Tests;

public sealed class KeepAliveServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 3, 0, 0, TimeSpan.Zero);

    private readonly IBrokerReader _broker = Substitute.For<IBrokerReader>();
    private readonly IBrokerSessionRepository _sessions = Substitute.For<IBrokerSessionRepository>();
    private readonly IOwnerNotifier _notifier = Substitute.For<IOwnerNotifier>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public KeepAliveServiceTests()
    {
        _clock.UtcNow.Returns(Now);
        _sessions.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new BrokerSessionState { Status = SessionStatus.Unknown });
    }

    private KeepAliveService CreateSut() => new(_broker, _sessions, _notifier, _clock);

    [Fact]
    public async Task TickAsync_PingSucceeds_MarksLiveAndRecordsKeepAlive()
    {
        _broker.KeepAliveAsync(Arg.Any<CancellationToken>()).Returns(true);

        await CreateSut().TickAsync();

        await _sessions.Received(1).SaveAsync(
            Arg.Is<BrokerSessionState>(s => s != null &&
                s.Status == SessionStatus.Live &&
                s.LastKeepAliveAt == Now &&
                s.ReauthAlertedAt == null),
            Arg.Any<CancellationToken>());
        await _notifier.DidNotReceive().NotifyAsync(
            Arg.Any<AlertKind>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TickAsync_RecoveryAfterLapse_ClearsReauthAlert()
    {
        _sessions.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new BrokerSessionState
            {
                Status = SessionStatus.Lapsed,
                ReauthAlertedAt = Now.AddMinutes(-15),
            });
        _broker.KeepAliveAsync(Arg.Any<CancellationToken>()).Returns(true);

        await CreateSut().TickAsync();

        await _sessions.Received(1).SaveAsync(
            Arg.Is<BrokerSessionState>(s => s != null &&
                s.Status == SessionStatus.Live &&
                s.ReauthAlertedAt == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TickAsync_PingFails_MarksLapsedAndAlertsOnce()
    {
        _broker.KeepAliveAsync(Arg.Any<CancellationToken>()).Returns(false);

        await CreateSut().TickAsync();

        await _notifier.Received(1).NotifyAsync(
            AlertKind.BrokerReauthRequired, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _sessions.Received(1).SaveAsync(
            Arg.Is<BrokerSessionState>(s => s != null &&
                s.Status == SessionStatus.Lapsed &&
                s.ReauthAlertedAt == Now),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TickAsync_PingThrows_TreatedAsFailureAndAlerts()
    {
        _broker.KeepAliveAsync(Arg.Any<CancellationToken>())
            .Returns<bool>(_ => throw new HttpRequestException("boom"));

        await CreateSut().TickAsync();

        await _notifier.Received(1).NotifyAsync(
            AlertKind.BrokerReauthRequired, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _sessions.Received(1).SaveAsync(
            Arg.Is<BrokerSessionState>(s => s != null && s.Status == SessionStatus.Lapsed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TickAsync_SecondFailure_DoesNotReAlert()
    {
        _sessions.GetAsync(Arg.Any<CancellationToken>())
            .Returns(new BrokerSessionState
            {
                Status = SessionStatus.Lapsed,
                ReauthAlertedAt = Now.AddMinutes(-5),
            });
        _broker.KeepAliveAsync(Arg.Any<CancellationToken>()).Returns(false);

        await CreateSut().TickAsync();

        await _notifier.DidNotReceive().NotifyAsync(
            Arg.Any<AlertKind>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _sessions.Received(1).SaveAsync(
            Arg.Is<BrokerSessionState>(s => s != null &&
                s.Status == SessionStatus.Lapsed &&
                s.ReauthAlertedAt == Now.AddMinutes(-5)),
            Arg.Any<CancellationToken>());
    }
}
