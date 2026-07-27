using NSubstitute;
using WiseWizard.Core.Abstractions;
using WiseWizard.Infrastructure.Ingestion;

namespace WiseWizard.Infrastructure.Tests;

public class RateLimiterTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 26, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ComputeWait_returns_zero_when_no_previous_request()
    {
        var wait = TokenBucketRateLimiter.ComputeWait(null, T0, TimeSpan.FromSeconds(1));

        Assert.Equal(TimeSpan.Zero, wait);
    }

    [Fact]
    public void ComputeWait_returns_remaining_when_within_interval()
    {
        var last = T0;
        var now = T0 + TimeSpan.FromMilliseconds(300);

        var wait = TokenBucketRateLimiter.ComputeWait(last, now, TimeSpan.FromSeconds(1));

        Assert.Equal(TimeSpan.FromMilliseconds(700), wait);
    }

    [Fact]
    public void ComputeWait_returns_zero_when_interval_already_elapsed()
    {
        var last = T0;
        var now = T0 + TimeSpan.FromSeconds(2);

        var wait = TokenBucketRateLimiter.ComputeWait(last, now, TimeSpan.FromSeconds(1));

        Assert.Equal(TimeSpan.Zero, wait);
    }

    [Theory]
    [InlineData(10, 100)]   // SEC: 10 req/s -> 100 ms
    [InlineData(1, 1000)]   // RSS/market: 1 req/s -> 1000 ms
    [InlineData(4, 250)]
    public void IntervalForRatePerSecond_maps_rate_to_interval(double rate, double expectedMs)
    {
        var interval = TokenBucketRateLimiter.IntervalForRatePerSecond(rate);

        Assert.Equal(expectedMs, interval.TotalMilliseconds, 3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void IntervalForRatePerSecond_rejects_non_positive_rate(double rate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TokenBucketRateLimiter.IntervalForRatePerSecond(rate));
    }

    [Fact]
    public async Task WaitAsync_first_call_does_not_delay()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(T0);
        var delays = new List<TimeSpan>();

        var limiter = new TokenBucketRateLimiter(clock, TimeSpan.FromSeconds(1), (d, _) =>
        {
            delays.Add(d);
            return Task.CompletedTask;
        });

        await limiter.WaitAsync("host");

        Assert.Empty(delays); // no delay invoked for the first request
    }

    [Fact]
    public async Task WaitAsync_second_call_within_interval_delays_remaining()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(T0, T0 + TimeSpan.FromMilliseconds(200));
        var delays = new List<TimeSpan>();

        var limiter = new TokenBucketRateLimiter(clock, TimeSpan.FromSeconds(1), (d, _) =>
        {
            delays.Add(d);
            return Task.CompletedTask;
        });

        await limiter.WaitAsync("host"); // reserves slot at T0
        await limiter.WaitAsync("host"); // 200 ms later -> wait 800 ms

        var delay = Assert.Single(delays);
        Assert.Equal(TimeSpan.FromMilliseconds(800), delay);
    }

    [Fact]
    public async Task WaitAsync_paces_each_host_independently()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(T0);
        var delays = new List<TimeSpan>();

        var limiter = new TokenBucketRateLimiter(clock, TimeSpan.FromSeconds(1), (d, _) =>
        {
            delays.Add(d);
            return Task.CompletedTask;
        });

        await limiter.WaitAsync("host-a");
        await limiter.WaitAsync("host-b");

        Assert.Empty(delays); // different hosts -> neither waits
    }

    [Fact]
    public async Task WaitAsync_uses_default_delay_when_none_injected()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(T0, T0 + TimeSpan.FromMilliseconds(999));

        // Tiny interval so the real Task.Delay path is exercised without slowing the suite.
        var limiter = new TokenBucketRateLimiter(clock, TimeSpan.FromMilliseconds(1000));

        await limiter.WaitAsync("host"); // reserve at T0
        // Second call requires ~1 ms real delay via the default Task.Delay.
        await limiter.WaitAsync("host");
    }
}
