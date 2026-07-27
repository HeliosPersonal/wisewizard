using System.Globalization;
using Dapper;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;

namespace WiseWizard.Infrastructure.Persistence;

/// <summary>
/// Dapper/PostgreSQL implementation of <see cref="IWatchlistRepository"/> over the <c>watchlist</c>
/// table. Tickers are stored as their normalized <see cref="Ticker.Value"/>; timestamps are
/// ISO-8601 UTC text. Note-length and other domain rules are enforced by the service, not here.
/// </summary>
public sealed class WatchlistRepository(IDbConnectionFactory connectionFactory) : IWatchlistRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<bool> AddAsync(WatchlistEntry entry, CancellationToken ct = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(ct);

        const string sql = """
            INSERT INTO watchlist (ticker, added_at, note)
            VALUES (@Ticker, @AddedAt, @Note)
            ON CONFLICT (ticker) DO NOTHING;
            """;

        var rows = await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                Ticker = entry.Ticker.Value,
                AddedAt = entry.AddedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                Note = entry.Note,
            },
            cancellationToken: ct));

        // ON CONFLICT DO NOTHING affects 0 rows when the ticker already exists (duplicate).
        return rows > 0;
    }

    public async Task<bool> RemoveAsync(Ticker ticker, CancellationToken ct = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(ct);

        const string sql = "DELETE FROM watchlist WHERE ticker = @Ticker;";

        var rows = await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { Ticker = ticker.Value },
            cancellationToken: ct));

        return rows > 0;
    }

    public async Task<IReadOnlyList<WatchlistEntry>> GetAllAsync(CancellationToken ct = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(ct);

        const string sql = """
            SELECT ticker AS Ticker, added_at AS AddedAt, note AS Note
            FROM watchlist
            ORDER BY added_at, ticker;
            """;

        var rows = await connection.QueryAsync<WatchlistRow>(new CommandDefinition(sql, cancellationToken: ct));

        return rows.Select(r => new WatchlistEntry
        {
            Ticker = Ticker.Create(r.Ticker),
            AddedAt = DateTimeOffset.Parse(r.AddedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            Note = r.Note,
        }).ToList();
    }

    public async Task<bool> ContainsAsync(Ticker ticker, CancellationToken ct = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(ct);

        const string sql = "SELECT EXISTS(SELECT 1 FROM watchlist WHERE ticker = @Ticker);";

        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            sql,
            new { Ticker = ticker.Value },
            cancellationToken: ct));
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(ct);

        const string sql = "SELECT COUNT(*) FROM watchlist;";

        return (int)await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, cancellationToken: ct));
    }

    private sealed record WatchlistRow
    {
        public string Ticker { get; init; } = string.Empty;
        public string AddedAt { get; init; } = string.Empty;
        public string? Note { get; init; }
    }
}
