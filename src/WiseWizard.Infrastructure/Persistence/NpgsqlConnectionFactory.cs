using Npgsql;

namespace WiseWizard.Infrastructure.Persistence;

/// <summary>
/// Opens connections to the domain PostgreSQL database at a configured connection string.
/// Foreign keys are always enforced by PostgreSQL, so no per-connection pragma is needed.
/// </summary>
public sealed class NpgsqlConnectionFactory(string connectionString) : IDbConnectionFactory
{
    private readonly string _connectionString = connectionString;

    public async Task<NpgsqlConnection> OpenAsync(CancellationToken ct = default)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
