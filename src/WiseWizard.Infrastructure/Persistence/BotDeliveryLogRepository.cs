using System.Globalization;
using Dapper;
using WiseWizard.Core.Abstractions;

namespace WiseWizard.Infrastructure.Persistence;

/// <summary>
/// Dapper/PostgreSQL implementation of <see cref="IBotDeliveryLog"/> over <c>bot_delivery_log</c>.
/// <see cref="TryMarkDeliveredAsync"/> uses <c>ON CONFLICT (event_key) DO NOTHING</c> against the
/// UNIQUE <c>event_key</c>: the first insert wins (returns true → send the alert) and any later insert
/// of the same key does nothing (returns false → suppress), making alert delivery idempotent across
/// restarts. Timestamps are ISO-8601 round-trippable ("O") UTC.
/// </summary>
public sealed class BotDeliveryLogRepository(IDbConnectionFactory factory) : IBotDeliveryLog
{
    private readonly IDbConnectionFactory _factory = factory;

    public async Task<bool> TryMarkDeliveredAsync(
        string eventKey,
        long? runId,
        DateTimeOffset deliveredAt,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventKey);

        await using var connection = await _factory.OpenAsync(ct);

        const string sql = """
            INSERT INTO bot_delivery_log (event_key, run_id, delivered_at)
            VALUES (@EventKey, @RunId, @DeliveredAt)
            ON CONFLICT (event_key) DO NOTHING;
            """;

        var rows = await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                EventKey = eventKey,
                RunId = runId,
                DeliveredAt = deliveredAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            },
            cancellationToken: ct));

        return rows == 1;
    }
}
