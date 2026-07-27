using System.Globalization;
using System.Text.Json;
using Dapper;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;

namespace WiseWizard.Infrastructure.Persistence;

/// <summary>
/// Dapper/PostgreSQL implementation of <see cref="IRunRepository"/> over the <c>runs</c> table.
/// <see cref="CreateAsync"/> inserts and returns the Run with its assigned autoincrement id.
/// <see cref="RunStatus"/> maps to/from lowercase text; the <c>batch_ids_json</c> column stores
/// the tier→batch-id map via System.Text.Json; timestamps are ISO-8601 round-trippable ("O") UTC.
/// </summary>
public sealed class RunRepository(IDbConnectionFactory factory) : IRunRepository
{
    private readonly IDbConnectionFactory _factory = factory;

    public async Task<Run> CreateAsync(Run run, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        await using var connection = await _factory.OpenAsync(ct);

        const string sql = """
            INSERT INTO runs
                (status, started_at, finished_at, batch_ids_json,
                 cost_cheap_usd, cost_synthesis_usd, cost_total_usd,
                 tokens_cheap, tokens_total, failure_reason)
            VALUES
                (@Status, @StartedAt, @FinishedAt, @BatchIdsJson,
                 @CostCheapUsd, @CostSynthesisUsd, @CostTotalUsd,
                 @TokensCheap, @TokensTotal, @FailureReason)
            RETURNING run_id;
            """;

        var newId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            sql, ToParams(run), cancellationToken: ct));

        return run with { RunId = newId };
    }

    public async Task UpdateAsync(Run run, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        await using var connection = await _factory.OpenAsync(ct);

        const string sql = """
            UPDATE runs SET
                status = @Status,
                started_at = @StartedAt,
                finished_at = @FinishedAt,
                batch_ids_json = @BatchIdsJson,
                cost_cheap_usd = @CostCheapUsd,
                cost_synthesis_usd = @CostSynthesisUsd,
                cost_total_usd = @CostTotalUsd,
                tokens_cheap = @TokensCheap,
                tokens_total = @TokensTotal,
                failure_reason = @FailureReason
            WHERE run_id = @RunId;
            """;

        await connection.ExecuteAsync(new CommandDefinition(sql, ToParams(run), cancellationToken: ct));
    }

    public async Task<Run?> GetAsync(long runId, CancellationToken ct = default)
    {
        await using var connection = await _factory.OpenAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<RunRow>(new CommandDefinition(
            SelectColumns + " WHERE run_id = @RunId;",
            new { RunId = runId },
            cancellationToken: ct));

        return row is null ? null : Map(row);
    }

    public async Task<Run?> GetLatestFinishedAsync(CancellationToken ct = default)
    {
        await using var connection = await _factory.OpenAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<RunRow>(new CommandDefinition(
            SelectColumns + " WHERE status = 'finished' ORDER BY finished_at DESC LIMIT 1;",
            cancellationToken: ct));

        return row is null ? null : Map(row);
    }

    public async Task<IReadOnlyList<Run>> GetResumableAsync(CancellationToken ct = default)
    {
        await using var connection = await _factory.OpenAsync(ct);

        var rows = await connection.QueryAsync<RunRow>(new CommandDefinition(
            SelectColumns + " WHERE status NOT IN ('finished','failed') ORDER BY started_at;",
            cancellationToken: ct));

        return rows.Select(Map).ToList();
    }

    private const string SelectColumns = """
        SELECT run_id AS RunId, status AS Status, started_at AS StartedAt, finished_at AS FinishedAt,
               batch_ids_json AS BatchIdsJson, cost_cheap_usd AS CostCheapUsd,
               cost_synthesis_usd AS CostSynthesisUsd, cost_total_usd AS CostTotalUsd,
               tokens_cheap AS TokensCheap, tokens_total AS TokensTotal, failure_reason AS FailureReason
        FROM runs
        """;

    private static object ToParams(Run run) => new
    {
        run.RunId,
        Status = ToToken(run.Status),
        StartedAt = run.StartedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        FinishedAt = run.FinishedAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        BatchIdsJson = JsonSerializer.Serialize(run.BatchIds),
        run.CostCheapUsd,
        run.CostSynthesisUsd,
        run.CostTotalUsd,
        run.TokensCheap,
        run.TokensTotal,
        run.FailureReason,
    };

    internal static string ToToken(RunStatus status) => status switch
    {
        RunStatus.Pending => "pending",
        RunStatus.Ingesting => "ingesting",
        RunStatus.Extracting => "extracting",
        RunStatus.Synthesizing => "synthesizing",
        RunStatus.Persisting => "persisting",
        RunStatus.Finished => "finished",
        RunStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown run status."),
    };

    internal static RunStatus FromToken(string token) => token switch
    {
        "pending" => RunStatus.Pending,
        "ingesting" => RunStatus.Ingesting,
        "extracting" => RunStatus.Extracting,
        "synthesizing" => RunStatus.Synthesizing,
        "persisting" => RunStatus.Persisting,
        "finished" => RunStatus.Finished,
        "failed" => RunStatus.Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(token), token, "Unknown run status token."),
    };

    /// <summary>Deserializes the persisted tier→batch-id map, tolerating null/blank/"null" JSON.</summary>
    internal static IReadOnlyDictionary<string, string> DeserializeBatchIds(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();

    private static Run Map(RunRow r) => new()
    {
        RunId = r.RunId,
        Status = FromToken(r.Status),
        StartedAt = DateTimeOffset.Parse(r.StartedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        FinishedAt = r.FinishedAt is null
            ? null
            : DateTimeOffset.Parse(r.FinishedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        BatchIds = DeserializeBatchIds(r.BatchIdsJson),
        CostCheapUsd = r.CostCheapUsd,
        CostSynthesisUsd = r.CostSynthesisUsd,
        CostTotalUsd = r.CostTotalUsd,
        TokensCheap = r.TokensCheap,
        TokensTotal = r.TokensTotal,
        FailureReason = r.FailureReason,
    };

    private sealed record RunRow
    {
        public long RunId { get; init; }
        public string Status { get; init; } = string.Empty;
        public string StartedAt { get; init; } = string.Empty;
        public string? FinishedAt { get; init; }
        public string BatchIdsJson { get; init; } = "{}";
        public decimal CostCheapUsd { get; init; }
        public decimal CostSynthesisUsd { get; init; }
        public decimal CostTotalUsd { get; init; }
        public long TokensCheap { get; init; }
        public long TokensTotal { get; init; }
        public string? FailureReason { get; init; }
    }
}
