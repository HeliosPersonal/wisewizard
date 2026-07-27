using Microsoft.Extensions.Options;
using NSubstitute;
using WiseWizard.Bot;
using WiseWizard.Core.Abstractions;

namespace WiseWizard.Bot.Tests;

public class TelegramOwnerNotifierTests
{
    private const long Owner = 42;
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    private sealed class Fixture
    {
        public RecordingGateway Gateway { get; } = new();
        public IBotDeliveryLog Log { get; } = Substitute.For<IBotDeliveryLog>();
        public TelegramOwnerNotifier Notifier { get; }

        public Fixture(bool firstDelivery = true)
        {
            var clock = Substitute.For<IClock>();
            clock.UtcNow.Returns(Now);
            Log.TryMarkDeliveredAsync(Arg.Any<string>(), Arg.Any<long?>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
                .Returns(firstDelivery);
            Notifier = new TelegramOwnerNotifier(
                Gateway, Log, clock,
                Options.Create(new BotOptions { OwnerChatId = Owner, BotToken = "t" }));
        }
    }

    [Fact]
    public async Task Sends_alert_to_owner_on_first_delivery()
    {
        var f = new Fixture(firstDelivery: true);

        await f.Notifier.NotifyAsync(AlertKind.RunFailed, "Run 7 did not complete");

        var sent = Assert.Single(f.Gateway.Sent);
        Assert.Equal(Owner, sent.ChatId);
        Assert.Contains("did not complete", sent.Text);
    }

    [Fact]
    public async Task Suppresses_duplicate_when_already_delivered()
    {
        var f = new Fixture(firstDelivery: false);

        await f.Notifier.NotifyAsync(AlertKind.RunFailed, "Run 7 did not complete");

        Assert.Empty(f.Gateway.Sent);
    }

    [Fact]
    public async Task Records_delivery_with_clock_time_before_sending()
    {
        var f = new Fixture(firstDelivery: true);

        await f.Notifier.NotifyAsync(AlertKind.BrokerReauthRequired, "re-auth needed");

        await f.Log.Received(1).TryMarkDeliveredAsync(
            Arg.Is<string>(k => k != null && k.StartsWith("session_lapse:")),
            null,
            Now,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_failed_uses_run_failed_event_key_prefix()
    {
        var f = new Fixture(firstDelivery: true);

        await f.Notifier.NotifyAsync(AlertKind.RunFailed, "Run 7 did not complete");

        await f.Log.Received(1).TryMarkDeliveredAsync(
            Arg.Is<string>(k => k != null && k.StartsWith("run_failed:")),
            null,
            Now,
            Arg.Any<CancellationToken>());
    }
}
