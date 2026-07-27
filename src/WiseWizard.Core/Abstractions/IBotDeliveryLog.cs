namespace WiseWizard.Core.Abstractions;

/// <summary>
/// Idempotency log for outbound Owner alerts. A stable <c>eventKey</c> (e.g.
/// <c>run_failed:&lt;run_id&gt;</c> or <c>session_lapse:&lt;lapse_started_at&gt;</c>) is recorded
/// on first successful delivery so the same event is never re-alerted after a process restart
/// (data-model.md §bot_delivery_log; seq-alert idempotency).
/// </summary>
public interface IBotDeliveryLog
{
    /// <summary>
    /// Atomically records that the given event has been delivered. Returns <c>true</c> when this
    /// call was the first to claim the <paramref name="eventKey"/> (the caller should send the
    /// alert), or <c>false</c> when the event was already delivered (the caller must suppress it).
    /// </summary>
    /// <param name="eventKey">Stable de-dup key for the alert-able event.</param>
    /// <param name="runId">Soft reference to the related Run, or null for session events.</param>
    /// <param name="deliveredAt">The delivery instant (supplied via <see cref="IClock"/>).</param>
    Task<bool> TryMarkDeliveredAsync(
        string eventKey,
        long? runId,
        DateTimeOffset deliveredAt,
        CancellationToken ct = default);
}
