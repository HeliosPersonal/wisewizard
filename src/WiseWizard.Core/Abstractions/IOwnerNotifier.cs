namespace WiseWizard.Core.Abstractions;

/// <summary>Kind of operational alert delivered to the Owner.</summary>
public enum AlertKind
{
    /// <summary>The Brokerage session lapsed and needs a manual 2FA re-auth.</summary>
    BrokerReauthRequired,

    /// <summary>A nightly Run failed.</summary>
    RunFailed,
}

/// <summary>
/// Delivers operational alerts to the Owner (implemented over Telegram). Kept in Core so the
/// broker session keeper and the pipeline can raise alerts without depending on the bot.
/// </summary>
public interface IOwnerNotifier
{
    Task NotifyAsync(AlertKind kind, string message, CancellationToken ct = default);
}
