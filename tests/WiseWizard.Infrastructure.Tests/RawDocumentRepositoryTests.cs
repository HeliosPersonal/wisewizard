using Dapper;
using WiseWizard.Core.Models;
using WiseWizard.Core.Services;
using WiseWizard.Infrastructure.Persistence;

namespace WiseWizard.Infrastructure.Tests;

public class RawDocumentRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    private static RawDocument Doc(
        long runId,
        Ticker ticker,
        SourceKind source,
        string content,
        DateTimeOffset? published = null,
        DateTimeOffset? fetchedAt = null,
        string? title = "Title",
        string? url = "https://x/1",
        string? hash = null)
        => new()
        {
            DocumentId = Guid.NewGuid().ToString("N"),
            RunId = runId,
            Ticker = ticker,
            Source = source,
            Url = url,
            Title = title ?? "Title",
            Content = content,
            PublishedAt = published,
            FetchedAt = fetchedAt ?? Now,
            ContentHash = hash ?? ContentHasher.Compute(title, content, url),
        };

    [Fact]
    public async Task Add_then_get_round_trips_all_fields()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new RawDocumentRepository(db);
        var t = Ticker.Create("AAPL");
        var doc = Doc(1, t, SourceKind.SecFiling, "content", published: Now - TimeSpan.FromDays(1));

        Assert.True(await repo.AddIfNewAsync(doc));

        var all = await repo.GetForRunAsync(1);
        var loaded = Assert.Single(all);
        Assert.Equal(doc.DocumentId, loaded.DocumentId);
        Assert.Equal(doc.RunId, loaded.RunId);
        Assert.Equal(t, loaded.Ticker);
        Assert.Equal(SourceKind.SecFiling, loaded.Source);
        Assert.Equal(doc.Url, loaded.Url);
        Assert.Equal(doc.Title, loaded.Title);
        Assert.Equal(doc.Content, loaded.Content);
        Assert.Equal(doc.PublishedAt, loaded.PublishedAt);
        Assert.Equal(doc.FetchedAt, loaded.FetchedAt);
        Assert.Equal(doc.ContentHash, loaded.ContentHash);
    }

    [Fact]
    public async Task Null_published_and_url_round_trip()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new RawDocumentRepository(db);
        var doc = Doc(1, Ticker.Create("AAPL"), SourceKind.MarketData, "snap", published: null, url: null);

        await repo.AddIfNewAsync(doc);

        var loaded = Assert.Single(await repo.GetForRunAsync(1));
        Assert.Null(loaded.PublishedAt);
        Assert.Null(loaded.Url);
    }

    [Theory]
    [InlineData(SourceKind.SecFiling)]
    [InlineData(SourceKind.News)]
    [InlineData(SourceKind.MarketData)]
    public async Task All_source_kinds_map_round_trip(SourceKind kind)
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new RawDocumentRepository(db);
        var doc = Doc(1, Ticker.Create("AAPL"), kind, "c-" + kind);

        await repo.AddIfNewAsync(doc);

        var loaded = Assert.Single(await repo.GetForRunAsync(1));
        Assert.Equal(kind, loaded.Source);
    }

    [Fact]
    public async Task AddIfNew_returns_false_on_same_run_and_content_hash()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new RawDocumentRepository(db);
        var t = Ticker.Create("AAPL");
        var first = Doc(1, t, SourceKind.News, "same", hash: "dup-hash");
        var second = Doc(1, t, SourceKind.News, "same", hash: "dup-hash");

        Assert.True(await repo.AddIfNewAsync(first));
        Assert.False(await repo.AddIfNewAsync(second));

        Assert.Single(await repo.GetForRunAsync(1));
    }

    [Fact]
    public async Task Same_content_hash_in_different_run_is_allowed()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new RawDocumentRepository(db);
        var t = Ticker.Create("AAPL");

        Assert.True(await repo.AddIfNewAsync(Doc(1, t, SourceKind.News, "same", hash: "h")));
        Assert.True(await repo.AddIfNewAsync(Doc(2, t, SourceKind.News, "same", hash: "h")));

        Assert.Single(await repo.GetForRunAsync(1));
        Assert.Single(await repo.GetForRunAsync(2));
    }

    [Fact]
    public async Task GetForRun_filters_by_ticker()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new RawDocumentRepository(db);
        var aapl = Ticker.Create("AAPL");
        var msft = Ticker.Create("MSFT");

        await repo.AddIfNewAsync(Doc(1, aapl, SourceKind.News, "a"));
        await repo.AddIfNewAsync(Doc(1, msft, SourceKind.News, "b"));

        var aaplDocs = await repo.GetForRunAsync(1, aapl);
        var loaded = Assert.Single(aaplDocs);
        Assert.Equal(aapl, loaded.Ticker);

        Assert.Equal(2, (await repo.GetForRunAsync(1)).Count);
    }

    [Fact]
    public async Task GetForRun_returns_empty_for_unknown_run()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new RawDocumentRepository(db);

        Assert.Empty(await repo.GetForRunAsync(999));
    }

    [Fact]
    public async Task DeleteOlderThan_removes_by_fetched_at_and_returns_count()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new RawDocumentRepository(db);
        var t = Ticker.Create("AAPL");

        var old = Doc(1, t, SourceKind.News, "old", fetchedAt: Now - TimeSpan.FromDays(100), hash: "h-old");
        var recent = Doc(1, t, SourceKind.News, "recent", fetchedAt: Now - TimeSpan.FromDays(10), hash: "h-recent");
        await repo.AddIfNewAsync(old);
        await repo.AddIfNewAsync(recent);

        var cutoff = Now - TimeSpan.FromDays(90);
        var removed = await repo.DeleteOlderThanAsync(cutoff);

        Assert.Equal(1, removed);
        var remaining = Assert.Single(await repo.GetForRunAsync(1));
        Assert.Equal("recent", remaining.Content);
    }

    [Fact]
    public async Task DeleteOlderThan_returns_zero_when_nothing_matches()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new RawDocumentRepository(db);
        await repo.AddIfNewAsync(Doc(1, Ticker.Create("AAPL"), SourceKind.News, "recent", fetchedAt: Now));

        Assert.Equal(0, await repo.DeleteOlderThanAsync(Now - TimeSpan.FromDays(90)));
    }

    [Fact]
    public async Task Add_rejects_unknown_source_enum_value()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new RawDocumentRepository(db);
        var doc = Doc(1, Ticker.Create("AAPL"), (SourceKind)999, "x");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.AddIfNewAsync(doc));
    }

    [Fact]
    public async Task Get_rejects_unknown_source_token_in_row()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new RawDocumentRepository(db);

        // Seed a row with an out-of-domain source token directly, bypassing the mapper.
        await using (var connection = await db.OpenAsync())
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO raw_documents
                    (document_id, run_id, ticker, source, url, title, content, published_at, fetched_at, content_hash)
                VALUES ('d1', 1, 'AAPL', 'bogus_source', NULL, 'T', 'C', NULL, @FetchedAt, 'h');
                """,
                new { FetchedAt = Now.ToString("O") });
        }

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.GetForRunAsync(1));
    }
}
