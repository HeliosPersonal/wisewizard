using WiseWizard.Core.Abstractions;

namespace WiseWizard.Infrastructure.Ingestion;

/// <summary>
/// Per-host request pacer enforcing a minimum interval between requests to a host (PRD §5 AC-03,
/// §6 — SEC ≤10 req/s ⇒ ≥100 ms; RSS/market ≤1 req/s/host ⇒ ≥1000 ms). Deterministic and testable:
/// time comes from <see cref="IClock"/> and the wait duration is a pure computation
/// (<see cref="ComputeWait"/>) that unit tests exercise directly; the actual delay is a thin
/// <see cref="Task.Delay(TimeSpan, CancellationToken)"/> injected via <c>delay</c> for tests.
/// </summary>
public sealed class TokenBucketRateLimiter : IRateLimiter
{
    private readonly IClock _clock;
    private readonly TimeSpan _minInterval;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    // Access is always serialized by _gate, so a plain Dictionary suffices.
    private readonly Dictionary<string, DateTimeOffset> _lastRequest = [];
    private readonly object _gate = new();

    /// <param name="clock">Time source (deterministic in tests).</param>
    /// <param name="minInterval">Minimum spacing between requests to the same host.</param>
    /// <param name="delay">
    /// The awaitable delay; defaults to <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.
    /// Override in tests to keep the suite fast and hermetic.
    /// </param>
    public TokenBucketRateLimiter(
        IClock clock,
        TimeSpan minInterval,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _clock = clock;
        _minInterval = minInterval;
        _delay = delay ?? Task.Delay;
    }

    /// <summary>
    /// The min interval derived from a per-second request ceiling (e.g. 10 req/s ⇒ 100 ms).
    /// </summary>
    public static TimeSpan IntervalForRatePerSecond(double requestsPerSecond)
    {
        if (requestsPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestsPerSecond), requestsPerSecond, "Rate must be positive.");
        }

        return TimeSpan.FromSeconds(1.0 / requestsPerSecond);
    }

    /// <summary>
    /// Pure computation: how long to wait before the next request to a host given the last request
    /// time, the current time, and the minimum interval. Never negative.
    /// </summary>
    public static TimeSpan ComputeWait(
        DateTimeOffset? lastRequest, DateTimeOffset now, TimeSpan minInterval)
    {
        if (lastRequest is null)
        {
            return TimeSpan.Zero;
        }

        var elapsed = now - lastRequest.Value;
        var remaining = minInterval - elapsed;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public async Task WaitAsync(string host, CancellationToken ct = default)
    {
        TimeSpan wait;
        lock (_gate)
        {
            var last = _lastRequest.TryGetValue(host, out var value) ? value : (DateTimeOffset?)null;
            var now = _clock.UtcNow;
            wait = ComputeWait(last, now, _minInterval);
            // Reserve the slot: schedule this request at now + wait so concurrent callers queue.
            _lastRequest[host] = now + wait;
        }

        if (wait > TimeSpan.Zero)
        {
            await _delay(wait, ct);
        }
    }
}
