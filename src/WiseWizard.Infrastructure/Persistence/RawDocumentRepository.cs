using System.Globalization;
using Dapper;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;

namespace WiseWizard.Infrastructure.Persistence;

/// <summary>
/// Dapper/PostgreSQL implementation of <see cref="IRawDocumentRepository"/> over the
/// <c>raw_documents</c> table. Dedup within a Run is enforced by the unique index
/// <c>ux_raw_documents_run_hash</c> on (run_id, content_hash): <see cref="AddIfNewAsync"/> uses
/// <c>ON CONFLICT DO NOTHING</c> and reports whether a row was actually stored. Timestamps are ISO-8601
/// UTC text; <see cref="SourceKind"/> maps to/from the fixed text tokens in data-model.md.
/// </summary>
public sealed class RawDocumentRepository(IDbConnectionFactory connectionFactory) : IRawDocumentRepository
{
    private const string SecToken = "sec_edgar";
    private const string NewsToken = "news_rss";
    private const string MarketToken = "market_data";

    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<bool> AddIfNewAsync(RawDocument document, CancellationToken ct = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(ct);

        // Target the (run_id, content_hash) dedup index explicitly so a genuine document_id PK
        // collision surfaces instead of being silently swallowed — dedup within a Run is the intent.
        const string sql = """
            INSERT INTO raw_documents
                (document_id, run_id, ticker, source, url, title, content, published_at, fetched_at, content_hash)
            VALUES
                (@DocumentId, @RunId, @Ticker, @Source, @Url, @Title, @Content, @PublishedAt, @FetchedAt, @ContentHash)
            ON CONFLICT (run_id, content_hash) DO NOTHING;
            """;

        var rows = await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                document.DocumentId,
                document.RunId,
                Ticker = document.Ticker.Value,
                Source = ToToken(document.Source),
                document.Url,
                document.Title,
                document.Content,
                PublishedAt = document.PublishedAt?.ToUniversalTime().ToString("O"),
                FetchedAt = document.FetchedAt.ToUniversalTime().ToString("O"),
                document.ContentHash,
            },
            cancellationToken: ct));

        // ON CONFLICT DO NOTHING affects 0 rows when the (run_id, content_hash) unique index would be
        // violated — i.e. a duplicate within the Run (AC-04).
        return rows > 0;
    }

    public async Task<IReadOnlyList<RawDocument>> GetForRunAsync(
        long runId, Ticker? ticker = null, CancellationToken ct = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(ct);

        var sql = """
            SELECT document_id AS DocumentId, run_id AS RunId, ticker AS Ticker, source AS Source,
                   url AS Url, title AS Title, content AS Content, published_at AS PublishedAt,
                   fetched_at AS FetchedAt, content_hash AS ContentHash
            FROM raw_documents
            WHERE run_id = @RunId
            """;

        if (ticker is not null)
        {
            sql += " AND ticker = @Ticker";
        }

        sql += " ORDER BY fetched_at, document_id;";

        var rows = await connection.QueryAsync<RawDocumentRow>(new CommandDefinition(
            sql,
            new { RunId = runId, Ticker = ticker?.Value },
            cancellationToken: ct));

        return rows.Select(Map).ToList();
    }

    public async Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(ct);

        const string sql = "DELETE FROM raw_documents WHERE fetched_at < @Cutoff;";

        return await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { Cutoff = cutoff.ToUniversalTime().ToString("O") },
            cancellationToken: ct));
    }

    private static string ToToken(SourceKind kind) => kind switch
    {
        SourceKind.SecFiling => SecToken,
        SourceKind.News => NewsToken,
        SourceKind.MarketData => MarketToken,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown source kind."),
    };

    private static SourceKind FromToken(string token) => token switch
    {
        SecToken => SourceKind.SecFiling,
        NewsToken => SourceKind.News,
        MarketToken => SourceKind.MarketData,
        _ => throw new ArgumentOutOfRangeException(nameof(token), token, "Unknown source token."),
    };

    private static RawDocument Map(RawDocumentRow r) => new()
    {
        DocumentId = r.DocumentId,
        RunId = r.RunId,
        Ticker = Ticker.Create(r.Ticker),
        Source = FromToken(r.Source),
        Url = r.Url,
        Title = r.Title,
        Content = r.Content,
        PublishedAt = r.PublishedAt is null
            ? null
            : DateTimeOffset.Parse(r.PublishedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        FetchedAt = DateTimeOffset.Parse(r.FetchedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        ContentHash = r.ContentHash,
    };

    private sealed record RawDocumentRow
    {
        public string DocumentId { get; init; } = string.Empty;
        public long RunId { get; init; }
        public string Ticker { get; init; } = string.Empty;
        public string Source { get; init; } = string.Empty;
        public string? Url { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public string? PublishedAt { get; init; }
        public string FetchedAt { get; init; } = string.Empty;
        public string ContentHash { get; init; } = string.Empty;
    }
}
