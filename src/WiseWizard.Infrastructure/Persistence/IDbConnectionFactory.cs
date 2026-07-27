using Npgsql;

namespace WiseWizard.Infrastructure.Persistence;

/// <summary>
/// Opens connections to the domain PostgreSQL database. Abstracted so repositories can be pointed
/// at a throwaway database (a Testcontainers Postgres instance) in integration tests.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>Opens a new connection. Caller disposes it.</summary>
    Task<NpgsqlConnection> OpenAsync(CancellationToken ct = default);
}
