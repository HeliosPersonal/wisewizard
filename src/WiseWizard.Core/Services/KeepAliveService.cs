using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;

namespace WiseWizard.Core.Services;

/// <summary>
/// Domain logic for a single keep-alive tick (not a hosted service — the hosted wrapper calls this
/// each interval). Pings the Broker: on success records the keep-alive and marks the session live
/// (clearing any re-auth alert on recovery); on failure marks the session lapsed and raises a
/// single re-auth alert per lapse (PRD §AC-04, §AC-09).
/// </summary>
public sealed class KeepAliveService(
    IBrokerReader broker,
    IBrokerSessionRepository sessions,
    IOwnerNotifier notifier,
    IClock clock)
{
    private const string ReauthMessage =
        "Brokerage session expired — tap 2FA in the Broker app to restore the Portfolio.";

    private readonly IBrokerReader _broker = broker;
    private readonly IBrokerSessionRepository _sessions = sessions;
    private readonly IOwnerNotifier _notifier = notifier;
    private readonly IClock _clock = clock;

    /// <summary>Performs one keep-alive tick. Never throws for an expected broker ping failure.</summary>
    public async Task TickAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var current = await _sessions.GetAsync(ct);

        bool live;
        try
        {
            live = await _broker.KeepAliveAsync(ct);
        }
        catch
        {
            live = false;
        }

        if (live)
        {
            await _sessions.SaveAsync(current with
            {
                Status = SessionStatus.Live,
                LastKeepAliveAt = now,
                ReauthAlertedAt = null,
            }, ct);
            return;
        }

        var alreadyAlerted = current.ReauthAlertedAt is not null;

        if (!alreadyAlerted)
        {
            await _notifier.NotifyAsync(AlertKind.BrokerReauthRequired, ReauthMessage, ct);
        }

        await _sessions.SaveAsync(current with
        {
            Status = SessionStatus.Lapsed,
            ReauthAlertedAt = alreadyAlerted ? current.ReauthAlertedAt : now,
        }, ct);
    }
}
