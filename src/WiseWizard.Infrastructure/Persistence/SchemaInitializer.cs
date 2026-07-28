using Npgsql;

namespace WiseWizard.Infrastructure.Persistence;

/// <summary>
/// Creates the domain schema (all feature tables) if it does not yet exist. Idempotent —
/// safe to run on every startup. The schema follows the per-feature data-model.md docs:
/// positions + broker_session (ibkr-portfolio-read), watchlist (watchlist-management),
/// raw_documents (data-ingestion), runs + extracted_facts + verdicts (nightly-research-pipeline).
/// PostgreSQL dialect (ADR-0007): money is <c>numeric</c>, identity keys are
/// <c>BIGINT GENERATED ALWAYS AS IDENTITY</c>, timestamps are ISO-8601 round-trippable text.
/// </summary>
public static class SchemaInitializer
{
    public const string CreateScript = """
        CREATE TABLE IF NOT EXISTS positions (
            ticker          TEXT    NOT NULL PRIMARY KEY,
            quantity        NUMERIC NOT NULL,
            avg_cost        NUMERIC NOT NULL,
            market_value    NUMERIC NOT NULL,
            unrealized_pnl  NUMERIC NOT NULL,
            currency        TEXT    NOT NULL DEFAULT 'USD',
            as_of           TEXT    NOT NULL
        );

        CREATE TABLE IF NOT EXISTS broker_session (
            id                        INTEGER PRIMARY KEY CHECK (id = 1),
            status                    TEXT    NOT NULL,
            last_snapshot_at          TEXT    NULL,
            last_refresh_attempt_at   TEXT    NULL,
            last_refresh_ok           TEXT    NULL,
            last_keepalive_at         TEXT    NULL,
            reauth_alerted_at         TEXT    NULL
        );

        CREATE TABLE IF NOT EXISTS watchlist (
            ticker    TEXT NOT NULL PRIMARY KEY,
            added_at  TEXT NOT NULL,
            note      TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS raw_documents (
            document_id   TEXT NOT NULL PRIMARY KEY,
            run_id        BIGINT NOT NULL,
            ticker        TEXT NOT NULL,
            source        TEXT NOT NULL,
            url           TEXT NULL,
            title         TEXT NOT NULL,
            content       TEXT NOT NULL,
            published_at  TEXT NULL,
            fetched_at    TEXT NOT NULL,
            content_hash  TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ux_raw_documents_run_hash ON raw_documents (run_id, content_hash);
        CREATE INDEX IF NOT EXISTS idx_raw_documents_run_ticker ON raw_documents (run_id, ticker);
        CREATE INDEX IF NOT EXISTS idx_raw_documents_fetched ON raw_documents (fetched_at);

        CREATE TABLE IF NOT EXISTS runs (
            run_id              BIGINT  GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            status              TEXT    NOT NULL,
            started_at          TEXT    NOT NULL,
            finished_at         TEXT    NULL,
            batch_ids_json      TEXT    NOT NULL DEFAULT '{}',
            cost_cheap_usd      NUMERIC NOT NULL DEFAULT 0,
            cost_synthesis_usd  NUMERIC NOT NULL DEFAULT 0,
            cost_total_usd      NUMERIC NOT NULL DEFAULT 0,
            tokens_cheap        BIGINT  NOT NULL DEFAULT 0,
            tokens_total        BIGINT  NOT NULL DEFAULT 0,
            failure_reason      TEXT    NULL
        );
        CREATE INDEX IF NOT EXISTS idx_runs_status_finished ON runs (status, finished_at DESC);

        CREATE TABLE IF NOT EXISTS extracted_facts (
            id           BIGINT  GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            run_id       BIGINT  NOT NULL REFERENCES runs (run_id),
            document_id  TEXT    NOT NULL REFERENCES raw_documents (document_id),
            ticker       TEXT    NOT NULL,
            fact         TEXT    NOT NULL,
            sentiment    TEXT    NOT NULL,
            materiality  TEXT    NOT NULL
        );
        CREATE INDEX IF NOT EXISTS idx_facts_run_ticker ON extracted_facts (run_id, ticker);

        CREATE TABLE IF NOT EXISTS verdicts (
            run_id                 BIGINT  NOT NULL REFERENCES runs (run_id),
            ticker                 TEXT    NOT NULL,
            signal                 TEXT    NOT NULL,
            summary_line           TEXT    NOT NULL,
            full_reasoning         TEXT    NOT NULL,
            sources_json           TEXT    NOT NULL,
            change_from_yesterday  TEXT    NOT NULL,
            created_at             TEXT    NOT NULL,
            PRIMARY KEY (run_id, ticker)
        );
        CREATE INDEX IF NOT EXISTS idx_verdicts_run_ticker ON verdicts (run_id, ticker);
        CREATE INDEX IF NOT EXISTS idx_verdicts_ticker_created ON verdicts (ticker, created_at DESC);

        CREATE TABLE IF NOT EXISTS bot_delivery_log (
            id            BIGINT  GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            event_key     TEXT    NOT NULL UNIQUE,
            run_id        BIGINT  NULL,
            delivered_at  TEXT    NOT NULL
        );
        """;

    /// <summary>
    /// Ensures the target database exists, creating it if necessary. Connects to the
    /// <c>postgres</c> maintenance database so it can run <c>CREATE DATABASE</c> outside
    /// a transaction. Safe to call on every startup.
    /// </summary>
    public static async Task EnsureDatabaseCreatedAsync(string connectionString, CancellationToken ct = default)
    {
        var csb = new NpgsqlConnectionStringBuilder(connectionString);
        var databaseName = csb.Database ?? throw new InvalidOperationException("Connection string must specify a Database.");

        csb.Database = "postgres";
        await using var conn = new NpgsqlConnection(csb.ToString());
        await conn.OpenAsync(ct);

        await using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(1) FROM pg_database WHERE datname = @name";
        checkCmd.Parameters.AddWithValue("name", databaseName);
        var exists = (long)(await checkCmd.ExecuteScalarAsync(ct))! > 0;

        if (!exists)
        {
            await using var createCmd = conn.CreateCommand();
            // Database names cannot be parameterised; the name comes from our own config.
            createCmd.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await createCmd.ExecuteNonQueryAsync(ct);
        }
    }

    /// <summary>Runs the create script against the given open connection.</summary>
    public static async Task InitializeAsync(NpgsqlConnection connection, CancellationToken ct = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = CreateScript;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Opens a connection from the factory and initializes the schema.</summary>
    public static async Task InitializeAsync(IDbConnectionFactory factory, CancellationToken ct = default)
    {
        await using var connection = await factory.OpenAsync(ct);
        await InitializeAsync(connection, ct);
    }
}
