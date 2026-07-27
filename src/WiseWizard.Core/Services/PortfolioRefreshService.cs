using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;

namespace WiseWizard.Core.Services;

/// <summary>
/// Orchestrates a read-only Portfolio refresh: checks the Brokerage session is live, reads the
/// current Positions, replaces the snapshot wholesale, and records session/freshness state.
/// A failed refresh (reader throws or session not live) retains the last known-good snapshot and
/// records the failed attempt. A lapsed session raises a single re-auth alert per lapse; recovery
/// clears the alert (PRD §AC-01, §AC-03, §AC-04, §AC-06, §AC-07).
/// </summary>
public sealed class PortfolioRefreshService(
    IBrokerReader broker,
    IPositionsRepository positions,
    IBrokerSessionRepository sessions,
    IOwnerNotifier notifier,
    IClock clock)
{
    private const string ReauthMessage =
        "Brokerage session expired — tap 2FA in the Broker app to restore the Portfolio.";

    private readonly IBrokerReader _broker = broker;
    private readonly IPositionsRepository _positions = positions;
    private readonly IBrokerSessionRepository _sessions = sessions;
    private readonly IOwnerNotifier _notifier = notifier;
    private readonly IClock _clock = clock;

    /// <summary>Attempts to refresh the Portfolio. Never throws for an expected broker/read failure.</summary>
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var current = await _sessions.GetAsync(ct);

        bool live;
        try
        {
            live = await _broker.IsSessionLiveAsync(ct);
        }
        catch
        {
            live = false;
        }

        if (!live)
        {
            await HandleNotLiveAsync(current, now, ct);
            return;
        }

        IReadOnlyList<Position> snapshot;
        try
        {
            snapshot = await _broker.ReadPositionsAsync(ct);
        }
        catch
        {
            // Reader failed mid-read: retain last-good, record a failed attempt, keep status live.
            await _sessions.SaveAsync(current with
            {
                LastRefreshAttemptAt = now,
                LastRefreshOk = false,
            }, ct);
            return;
        }

        await _positions.ReplaceSnapshotAsync(snapshot, ct);

        await _sessions.SaveAsync(current with
        {
            Status = SessionStatus.Live,
            LastSnapshotAt = now,
            LastRefreshAttemptAt = now,
            LastRefreshOk = true,
            ReauthAlertedAt = null,
        }, ct);
    }

    private async Task HandleNotLiveAsync(BrokerSessionState current, DateTimeOffset now, CancellationToken ct)
    {
        var alreadyAlerted = current.ReauthAlertedAt is not null;

        if (!alreadyAlerted)
        {
            await _notifier.NotifyAsync(AlertKind.BrokerReauthRequired, ReauthMessage, ct);
        }

        await _sessions.SaveAsync(current with
        {
            Status = SessionStatus.Lapsed,
            LastRefreshAttemptAt = now,
            LastRefreshOk = false,
            ReauthAlertedAt = alreadyAlerted ? current.ReauthAlertedAt : now,
        }, ct);
    }
}
