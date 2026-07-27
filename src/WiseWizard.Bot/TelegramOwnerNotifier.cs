using Microsoft.Extensions.Options;
using WiseWizard.Bot.Formatting;
using WiseWizard.Core.Abstractions;

namespace WiseWizard.Bot;

/// <summary>
/// Implements <see cref="IOwnerNotifier"/> over the Telegram gateway (AC-07/AC-08). Delivery is
/// made idempotent through <see cref="IBotDeliveryLog"/>: a stable event key derived from the
/// alert kind and message is claimed before sending, so the same Run-failure or session-lapse
/// event is never re-sent after a process restart (seq-alert idempotency).
/// </summary>
public sealed class TelegramOwnerNotifier(
    ITelegramGateway gateway,
    IBotDeliveryLog deliveryLog,
    IClock clock,
    IOptions<BotOptions> options)
    : IOwnerNotifier
{
    private readonly ITelegramGateway _gateway = gateway;
    private readonly IBotDeliveryLog _deliveryLog = deliveryLog;
    private readonly IClock _clock = clock;
    private readonly long _ownerChatId = options.Value.OwnerChatId;

    public async Task NotifyAsync(AlertKind kind, string message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var eventKey = $"{KindToken(kind)}:{message}";

        var firstDelivery = await _deliveryLog.TryMarkDeliveredAsync(eventKey, null, _clock.UtcNow, ct);
        if (!firstDelivery)
        {
            return; // Already delivered — suppress the duplicate.
        }

        await _gateway.SendTextAsync(_ownerChatId, TelegramText.Escape(message), null, ct);
    }

    private static string KindToken(AlertKind kind) => kind switch
    {
        AlertKind.RunFailed => "run_failed",
        AlertKind.BrokerReauthRequired => "session_lapse",
        _ => "alert",
    };
}
