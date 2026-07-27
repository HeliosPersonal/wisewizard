using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;
using WiseWizard.Core.Services;

namespace WiseWizard.Core.Tests;

public class IngestionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 0, 0, 0, TimeSpan.Zero);

    private static IClock ClockAt(DateTimeOffset now)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(now);
        return clock;
    }

    private static RawDocument Doc(Ticker t, long runId, SourceKind kind, string content, DateTimeOffset? published)
        => new()
        {
            DocumentId = Guid.NewGuid().ToString("N"),
            RunId = runId,
            Ticker = t,
            Source = kind,
            Url = "https://x/" + content,
            Title = "T-" + content,
            Content = content,
            PublishedAt = published,
            FetchedAt = Now,
            ContentHash = ContentHasher.Compute("T-" + content, content),
        };

    private static IDataSource SourceReturning(SourceKind kind, IReadOnlyList<RawDocument> docs)
    {
        var source = Substitute.For<IDataSource>();
        source.Kind.Returns(kind);
        source.FetchAsync(Arg.Any<Ticker>(), Arg.Any<long>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(docs);
        return source;
    }

    [Fact]
    public async Task Happy_path_collects_from_all_sources_and_stores()
    {
        var t = Ticker.Create("AAPL");
        var sec = SourceReturning(SourceKind.SecFiling, [Doc(t, 1, SourceKind.SecFiling, "sec1", Now)]);
        var news = SourceReturning(SourceKind.News, [Doc(t, 1, SourceKind.News, "news1", Now)]);
        var mkt = SourceReturning(SourceKind.MarketData, [Doc(t, 1, SourceKind.MarketData, "mkt1", Now)]);

        var repo = Substitute.For<IRawDocumentRepository>();
        repo.AddIfNewAsync(Arg.Any<RawDocument>(), Arg.Any<CancellationToken>()).Returns(true);

        var service = new IngestionService([sec, news, mkt], repo, ClockAt(Now), NullLogger<IngestionService>.Instance);

        var summary = await service.IngestAsync(1, [t]);

        Assert.Equal(3, summary.Results.Count);
        Assert.Empty(summary.Gaps);
        Assert.Equal(3, summary.TotalStored);
        Assert.Equal(0, summary.TotalSkipped);
        await repo.Received(3).AddIfNewAsync(Arg.Any<RawDocument>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Source_that_throws_records_gap_and_others_continue()
    {
        var t = Ticker.Create("AAPL");

        var sec = Substitute.For<IDataSource>();
        sec.Kind.Returns(SourceKind.SecFiling);
        sec.FetchAsync(Arg.Any<Ticker>(), Arg.Any<long>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<RawDocument>>>(_ => throw new HttpRequestException("unreachable"));

        var news = SourceReturning(SourceKind.News, [Doc(t, 1, SourceKind.News, "news1", Now)]);

        var repo = Substitute.For<IRawDocumentRepository>();
        repo.AddIfNewAsync(Arg.Any<RawDocument>(), Arg.Any<CancellationToken>()).Returns(true);

        var service = new IngestionService([sec, news], repo, ClockAt(Now), NullLogger<IngestionService>.Instance);

        var summary = await service.IngestAsync(1, [t]);

        var gap = Assert.Single(summary.Gaps);
        Assert.Equal(SourceKind.SecFiling, gap.Source);
        Assert.Equal(t, gap.Ticker);
        Assert.Equal(1, gap.RunId);
        Assert.Equal("unreachable", gap.Reason);

        // News source still produced a result and stored.
        var result = Assert.Single(summary.Results);
        Assert.Equal(SourceKind.News, result.Source);
        Assert.Equal(1, result.Stored);
    }

    [Fact]
    public async Task Duplicate_within_run_is_skipped_not_stored()
    {
        var t = Ticker.Create("AAPL");
        var news = SourceReturning(SourceKind.News,
            [Doc(t, 1, SourceKind.News, "a", Now), Doc(t, 1, SourceKind.News, "b", Now)]);

        var repo = Substitute.For<IRawDocumentRepository>();
        repo.AddIfNewAsync(Arg.Any<RawDocument>(), Arg.Any<CancellationToken>())
            .Returns(true, false); // first stored, second a duplicate

        var service = new IngestionService([news], repo, ClockAt(Now), NullLogger<IngestionService>.Instance);

        var summary = await service.IngestAsync(1, [t]);

        var result = Assert.Single(summary.Results);
        Assert.Equal(1, result.Stored);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(2, result.Fetched);
    }

    [Fact]
    public async Task Cap_enforced_at_15_docs_per_source_per_ticker()
    {
        var t = Ticker.Create("AAPL");
        var many = Enumerable.Range(0, 25)
            .Select(i => Doc(t, 1, SourceKind.News, "n" + i, Now))
            .ToList();
        var news = SourceReturning(SourceKind.News, many);

        var repo = Substitute.For<IRawDocumentRepository>();
        repo.AddIfNewAsync(Arg.Any<RawDocument>(), Arg.Any<CancellationToken>()).Returns(true);

        var service = new IngestionService([news], repo, ClockAt(Now), NullLogger<IngestionService>.Instance);

        var summary = await service.IngestAsync(1, [t]);

        var result = Assert.Single(summary.Results);
        Assert.Equal(IngestionService.MaxDocsPerSourcePerTicker, result.Fetched);
        Assert.Equal(15, result.Stored);
        await repo.Received(15).AddIfNewAsync(Arg.Any<RawDocument>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Documents_older_than_lookback_are_discarded()
    {
        var t = Ticker.Create("AAPL");
        var old = Doc(t, 1, SourceKind.News, "old", Now - TimeSpan.FromDays(20));
        var fresh = Doc(t, 1, SourceKind.News, "fresh", Now - TimeSpan.FromDays(1));
        var undated = Doc(t, 1, SourceKind.News, "undated", null);
        var news = SourceReturning(SourceKind.News, [old, fresh, undated]);

        var repo = Substitute.For<IRawDocumentRepository>();
        repo.AddIfNewAsync(Arg.Any<RawDocument>(), Arg.Any<CancellationToken>()).Returns(true);

        var service = new IngestionService([news], repo, ClockAt(Now), NullLogger<IngestionService>.Instance);

        var summary = await service.IngestAsync(1, [t]);

        var result = Assert.Single(summary.Results);
        // old dropped; fresh + undated kept.
        Assert.Equal(2, result.Fetched);
        Assert.Equal(2, result.Stored);
    }

    [Fact]
    public async Task Empty_universe_collects_nothing()
    {
        var news = SourceReturning(SourceKind.News, [Doc(Ticker.Create("AAPL"), 1, SourceKind.News, "n", Now)]);
        var repo = Substitute.For<IRawDocumentRepository>();

        var service = new IngestionService([news], repo, ClockAt(Now), NullLogger<IngestionService>.Instance);

        var summary = await service.IngestAsync(1, []);

        Assert.Empty(summary.Results);
        Assert.Empty(summary.Gaps);
        Assert.Equal(0, summary.TotalStored);
        await news.DidNotReceive().FetchAsync(
            Arg.Any<Ticker>(), Arg.Any<long>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await repo.DidNotReceive().AddIfNewAsync(Arg.Any<RawDocument>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ticker_with_zero_docs_is_recorded_not_a_failure()
    {
        var t = Ticker.Create("AAPL");
        var news = SourceReturning(SourceKind.News, []);
        var repo = Substitute.For<IRawDocumentRepository>();

        var service = new IngestionService([news], repo, ClockAt(Now), NullLogger<IngestionService>.Instance);

        var summary = await service.IngestAsync(1, [t]);

        var result = Assert.Single(summary.Results);
        Assert.Equal(0, result.Fetched);
        Assert.Equal(0, result.Stored);
        Assert.Equal(0, result.Skipped);
        Assert.Empty(summary.Gaps);
    }

    [Fact]
    public async Task Cancellation_before_fetch_throws()
    {
        var t = Ticker.Create("AAPL");
        var news = SourceReturning(SourceKind.News, []);
        var repo = Substitute.For<IRawDocumentRepository>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var service = new IngestionService([news], repo, ClockAt(Now), NullLogger<IngestionService>.Instance);

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.IngestAsync(1, [t], cts.Token));
    }

    [Fact]
    public async Task Cancellation_during_fetch_propagates_and_is_not_a_gap()
    {
        var t = Ticker.Create("AAPL");
        using var cts = new CancellationTokenSource();

        var news = Substitute.For<IDataSource>();
        news.Kind.Returns(SourceKind.News);
        news.FetchAsync(Arg.Any<Ticker>(), Arg.Any<long>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<RawDocument>>>(_ =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            });

        var repo = Substitute.For<IRawDocumentRepository>();
        var service = new IngestionService([news], repo, ClockAt(Now), NullLogger<IngestionService>.Instance);

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.IngestAsync(1, [t], cts.Token));
    }
}
