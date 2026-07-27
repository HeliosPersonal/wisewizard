using System.Globalization;
using Dapper;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;

namespace WiseWizard.Infrastructure.Persistence;

/// <summary>
/// Dapper/PostgreSQL persistence for the <c>positions</c> snapshot table. The snapshot is overwritten
/// wholesale on each successful refresh (delete-all-then-insert inside one transaction). Money is
/// stored as PostgreSQL <c>numeric</c> and mapped to/from <see cref="decimal"/>; timestamps are ISO-8601
/// round-trippable ("O") UTC TEXT.
/// </summary>
public sealed class PositionsRepository(IDbConnectionFactory factory) : IPositionsRepository
{
    private readonly IDbConnectionFactory _factory = factory;

    public async Task ReplaceSnapshotAsync(IReadOnlyList<Position> positions, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(positions);

        await using var connection = await _factory.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM positions;",
            transaction: transaction,
            cancellationToken: ct));

        const string insertSql = """
            INSERT INTO positions (ticker, quantity, avg_cost, market_value, unrealized_pnl, currency, as_of)
            VALUES (@Ticker, @Quantity, @AvgCost, @MarketValue, @UnrealizedPnl, @Currency, @AsOf);
            """;

        foreach (var position in positions)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                insertSql,
                new
                {
                    Ticker = position.Ticker.Value,
                    position.Quantity,
                    position.AvgCost,
                    position.MarketValue,
                    position.UnrealizedPnl,
                    position.Currency,
                    AsOf = position.AsOf.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                },
                transaction: transaction,
                cancellationToken: ct));
        }

        await transaction.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<Position>> GetCurrentAsync(CancellationToken ct = default)
    {
        await using var connection = await _factory.OpenAsync(ct);

        var rows = await connection.QueryAsync<PositionRow>(new CommandDefinition(
            "SELECT ticker, quantity, avg_cost, market_value, unrealized_pnl, currency, as_of FROM positions ORDER BY ticker;",
            cancellationToken: ct));

        return rows.Select(r => r.ToPosition()).ToList();
    }

    public async Task<IReadOnlyList<Ticker>> GetTickersAsync(CancellationToken ct = default)
    {
        await using var connection = await _factory.OpenAsync(ct);

        var symbols = await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT ticker FROM positions ORDER BY ticker;",
            cancellationToken: ct));

        return symbols.Select(Ticker.Create).ToList();
    }

    private sealed record PositionRow
    {
        public string ticker { get; init; } = string.Empty;
        public decimal quantity { get; init; }
        public decimal avg_cost { get; init; }
        public decimal market_value { get; init; }
        public decimal unrealized_pnl { get; init; }
        public string currency { get; init; } = "USD";
        public string as_of { get; init; } = string.Empty;

        public Position ToPosition() => new()
        {
            Ticker = Ticker.Create(ticker),
            Quantity = quantity,
            AvgCost = avg_cost,
            MarketValue = market_value,
            UnrealizedPnl = unrealized_pnl,
            Currency = currency,
            AsOf = DateTimeOffset.Parse(as_of, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        };
    }
}
