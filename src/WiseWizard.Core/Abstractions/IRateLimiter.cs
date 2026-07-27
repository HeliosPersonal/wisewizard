namespace WiseWizard.Core.Abstractions;

/// <summary>
/// Paces outbound requests to a Source host so the System stays within each Source's allowed
/// access rate (PRD §6 — SEC ≤10 req/s, RSS/market ≤1 req/s/host; PRD §5 AC-03). Callers
/// <c>await</c> <see cref="WaitAsync"/> immediately before each request to a host; the limiter
/// blocks just long enough to keep the per-host request rate within the configured interval.
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Waits until it is polite to issue the next request to <paramref name="host"/>, then returns.
    /// </summary>
    Task WaitAsync(string host, CancellationToken ct = default);
}
