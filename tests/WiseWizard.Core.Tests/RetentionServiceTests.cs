using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Services;

namespace WiseWizard.Core.Tests;

public class RetentionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 0, 0, 0, TimeSpan.Zero);

    private static IClock ClockAt(DateTimeOffset now)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(now);
        return clock;
    }

    [Fact]
    public async Task Cleanup_deletes_older_than_90_days_and_returns_count()
    {
        var repo = Substitute.For<IRawDocumentRepository>();
        repo.DeleteOlderThanAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(7);

        var service = new RetentionService(repo, ClockAt(Now), NullLogger<RetentionService>.Instance);

        var removed = await service.CleanupAsync();

        Assert.Equal(7, removed);
        var expectedCutoff = Now - TimeSpan.FromDays(90);
        await repo.Received(1).DeleteOlderThanAsync(expectedCutoff, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cleanup_with_nothing_to_remove_returns_zero()
    {
        var repo = Substitute.For<IRawDocumentRepository>();
        repo.DeleteOlderThanAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(0);

        var service = new RetentionService(repo, ClockAt(Now), NullLogger<RetentionService>.Instance);

        Assert.Equal(0, await service.CleanupAsync());
    }

    [Fact]
    public void RetentionWindow_is_90_days()
    {
        Assert.Equal(TimeSpan.FromDays(90), RetentionService.RetentionWindow);
    }
}
