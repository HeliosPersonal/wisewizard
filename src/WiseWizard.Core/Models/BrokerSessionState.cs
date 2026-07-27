namespace WiseWizard.Core.Models;

/// <summary>Brokerage session lifecycle state.</summary>
public enum SessionStatus
{
    Unknown,
    Live,
    Lapsed,
}

/// <summary>
/// Singleton state tracking the Brokerage session and the freshness of the last
/// known-good Portfolio snapshot, so the Owner can be shown Portfolio age and re-auth state.
/// </summary>
public sealed record BrokerSessionState
{
    public required SessionStatus Status { get; init; }

    /// <summary>UTC of the last successful Portfolio refresh (= positions.as_of). Null until first success.</summary>
    public DateTimeOffset? LastSnapshotAt { get; init; }

    public DateTimeOffset? LastRefreshAttemptAt { get; init; }
    public bool? LastRefreshOk { get; init; }
    public DateTimeOffset? LastKeepAliveAt { get; init; }

    /// <summary>UTC the Owner was last alerted to re-auth; cleared on recovery. Guards single-alert-per-lapse.</summary>
    public DateTimeOffset? ReauthAlertedAt { get; init; }
}
