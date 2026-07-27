using System.Globalization;
using System.Text.Json;
using Dapper;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;

namespace WiseWizard.Infrastructure.Persistence;

/// <summary>
/// Dapper/PostgreSQL implementation of <see cref="IVerdictRepository"/> over the <c>verdicts</c> table.
/// <see cref="UpsertAsync"/> uses <c>ON CONFLICT (run_id, ticker) DO UPDATE</c> on the composite PK
/// (run_id, ticker) so a resumed Run cannot create duplicate Verdicts (AC-08). The cited document ids
/// are stored as a JSON array in <c>sources_json</c>; the Signal maps to/from its lowercase token;
/// timestamps are ISO-8601 round-trippable ("O") UTC.
/// </summary>
public sealed class VerdictRepository(IDbConnectionFactory factory) : IVerdictRepository
{
    private readonly IDbConnectionFactory _factory = factory;

    public async Task UpsertAsync(Verdict verdict, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(verdict);

        await using var connection = await _factory.OpenAsync(ct);

        const string sql = """
            INSERT INTO verdicts
                (run_id, ticker, signal, summary_line, full_reasoning,
                 sources_json, change_from_yesterday, created_at)
            VALUES
                (@RunId, @Ticker, @Signal, @SummaryLine, @FullReasoning,
                 @SourcesJson, @ChangeFromYesterday, @CreatedAt)
            ON CONFLICT (run_id, ticker) DO UPDATE SET
                signal = excluded.signal,
                summary_line = excluded.summary_line,
                full_reasoning = excluded.full_reasoning,
                sources_json = excluded.sources_json,
                change_from_yesterday = excluded.change_from_yesterday,
                created_at = excluded.created_at;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                verdict.RunId,
                Ticker = verdict.Ticker.Value,
                Signal = verdict.Signal.ToToken(),
                verdict.SummaryLine,
                verdict.FullReasoning,
                SourcesJson = JsonSerializer.Serialize(verdict.Sources),
                verdict.ChangeFromYesterday,
                CreatedAt = verdict.CreatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            },
            cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Verdict>> GetForRunAsync(long runId, CancellationToken ct = default)
    {
        await using var connection = await _factory.OpenAsync(ct);

        var rows = await connection.QueryAsync<VerdictRow>(new CommandDefinition(
            SelectColumns + " WHERE run_id = @RunId ORDER BY ticker;",
            new { RunId = runId },
            cancellationToken: ct));

        return rows.Select(Map).ToList();
    }

    public async Task<Verdict?> GetAsync(long runId, Ticker ticker, CancellationToken ct = default)
    {
        await using var connection = await _factory.OpenAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<VerdictRow>(new CommandDefinition(
            SelectColumns + " WHERE run_id = @RunId AND ticker = @Ticker;",
            new { RunId = runId, Ticker = ticker.Value },
            cancellationToken: ct));

        return row is null ? null : Map(row);
    }

    public async Task<Verdict?> GetPreviousAsync(Ticker ticker, long beforeRunId, CancellationToken ct = default)
    {
        await using var connection = await _factory.OpenAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<VerdictRow>(new CommandDefinition(
            SelectColumns +
            " WHERE ticker = @Ticker AND run_id < @BeforeRunId ORDER BY created_at DESC, run_id DESC LIMIT 1;",
            new { Ticker = ticker.Value, BeforeRunId = beforeRunId },
            cancellationToken: ct));

        return row is null ? null : Map(row);
    }

    private const string SelectColumns = """
        SELECT run_id AS RunId, ticker AS Ticker, signal AS Signal, summary_line AS SummaryLine,
               full_reasoning AS FullReasoning, sources_json AS SourcesJson,
               change_from_yesterday AS ChangeFromYesterday, created_at AS CreatedAt
        FROM verdicts
        """;

    /// <summary>Deserializes the cited-sources JSON array, tolerating null/blank/"null" JSON.</summary>
    internal static IReadOnlyList<string> DeserializeSources(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<string>>(json) ?? [];

    private static Verdict Map(VerdictRow r) => new()
    {
        RunId = r.RunId,
        Ticker = Ticker.Create(r.Ticker),
        Signal = SignalExtensions.ParseSignal(r.Signal),
        SummaryLine = r.SummaryLine,
        FullReasoning = r.FullReasoning,
        Sources = DeserializeSources(r.SourcesJson),
        ChangeFromYesterday = r.ChangeFromYesterday,
        CreatedAt = DateTimeOffset.Parse(r.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
    };

    private sealed record VerdictRow
    {
        public long RunId { get; init; }
        public string Ticker { get; init; } = string.Empty;
        public string Signal { get; init; } = string.Empty;
        public string SummaryLine { get; init; } = string.Empty;
        public string FullReasoning { get; init; } = string.Empty;
        public string SourcesJson { get; init; } = "[]";
        public string ChangeFromYesterday { get; init; } = string.Empty;
        public string CreatedAt { get; init; } = string.Empty;
    }
}
