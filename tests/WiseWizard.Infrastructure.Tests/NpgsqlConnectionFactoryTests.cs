using Dapper;
using WiseWizard.Infrastructure.Persistence;

namespace WiseWizard.Infrastructure.Tests;

public class NpgsqlConnectionFactoryTests
{
    [Fact]
    public async Task OpenAsync_returns_open_connection()
    {
        await using var db = await TestDatabase.CreateAsync();

        // TestDatabase is itself an IDbConnectionFactory; opening yields a live connection.
        await using var connection = await db.OpenAsync();

        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }

    [Fact]
    public async Task OpenAsync_yields_usable_connection_for_queries()
    {
        await using var db = await TestDatabase.CreateAsync();
        await using var connection = await db.OpenAsync();

        var result = await connection.ExecuteScalarAsync<long>("SELECT 42;");
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task NpgsqlConnectionFactory_opens_a_connection_at_the_given_string()
    {
        // Exercise the production factory type directly against the test container's database.
        await using var db = await TestDatabase.CreateAsync();

        var factory = new NpgsqlConnectionFactory(db.ConnectionString);
        await using var connection = await factory.OpenAsync();

        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
        var result = await connection.ExecuteScalarAsync<long>("SELECT 1;");
        Assert.Equal(1, result);
    }
}
