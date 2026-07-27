using Npgsql;
using Testcontainers.PostgreSql;
using WiseWizard.Infrastructure.Persistence;

namespace WiseWizard.Infrastructure.Tests;

/// <summary>
/// A throwaway PostgreSQL database for integration tests. One PostgreSQL container is started once
/// for the whole test run (lazily, on first use) and torn down by Testcontainers' resource reaper
/// when the process exits. Each <see cref="CreateAsync"/> call provisions a fresh, uniquely-named
/// database inside that container with the schema initialized, so parallel tests never share state.
/// Dispose drops the database. Requires Docker to be available locally (CI provides it).
/// </summary>
public sealed class TestDatabase : IDbConnectionFactory, IAsyncDisposable
{
    private static readonly SemaphoreSlim ContainerGate = new(1, 1);
    private static PostgreSqlContainer? _container;

    private readonly string _connectionString;
    private readonly string _databaseName;

    private TestDatabase(string connectionString, string databaseName)
    {
        _connectionString = connectionString;
        _databaseName = databaseName;
    }

    /// <summary>The full connection string (including credentials) for this test database.</summary>
    public string ConnectionString => _connectionString;

    /// <summary>Creates a fresh, schema-initialized database unique to this instance.</summary>
    public static async Task<TestDatabase> CreateAsync()
    {
        var container = await GetContainerAsync();

        // Unique database name per instance so parallel tests do not share state.
        var databaseName = $"wwtest_{Guid.NewGuid():N}";

        // Connect to the container's default database to issue CREATE DATABASE (which cannot run
        // inside a transaction, so it is executed on its own).
        await using (var admin = new NpgsqlConnection(container.GetConnectionString()))
        {
            await admin.OpenAsync();
            await using var cmd = admin.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE \"{databaseName}\";";
            await cmd.ExecuteNonQueryAsync();
        }

        var connectionString = new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Database = databaseName,
        }.ConnectionString;

        var db = new TestDatabase(connectionString, databaseName);
        await SchemaInitializer.InitializeAsync(db);
        return db;
    }

    public async Task<NpgsqlConnection> OpenAsync(CancellationToken ct = default)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    public async ValueTask DisposeAsync()
    {
        var container = _container;
        if (container is null)
        {
            return;
        }

        // Clear only THIS database's pooled connections (not ClearAllPools, which is process-global
        // and would churn other tests' pools under xUnit parallelism), then drop with FORCE so the
        // terminate+drop is atomic (no race with a connection re-opened between the two — Postgres 13+).
        await using var idle = new NpgsqlConnection(_connectionString);
        NpgsqlConnection.ClearPool(idle);

        await using var admin = new NpgsqlConnection(container.GetConnectionString());
        await admin.OpenAsync();

        await using var drop = admin.CreateCommand();
        drop.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE);";
        await drop.ExecuteNonQueryAsync();
    }

    private static async Task<PostgreSqlContainer> GetContainerAsync()
    {
        if (_container is not null)
        {
            return _container;
        }

        await ContainerGate.WaitAsync();
        try
        {
            if (_container is null)
            {
                var container = new PostgreSqlBuilder("postgres:17-alpine")
                    .Build();
                await container.StartAsync();
                _container = container;
            }
        }
        finally
        {
            ContainerGate.Release();
        }

        return _container;
    }
}
