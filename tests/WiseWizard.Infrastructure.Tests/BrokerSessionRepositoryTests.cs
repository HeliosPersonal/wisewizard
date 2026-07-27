using WiseWizard.Core.Models;
using WiseWizard.Infrastructure.Persistence;

namespace WiseWizard.Infrastructure.Tests;

public sealed class BrokerSessionRepositoryTests
{
    private static readonly DateTimeOffset T = new(2026, 7, 26, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Get_WhenNoRow_ReturnsUnknownDefault()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new BrokerSessionRepository(db);

        var state = await repo.GetAsync();

        Assert.Equal(SessionStatus.Unknown, state.Status);
        Assert.Null(state.LastSnapshotAt);
        Assert.Null(state.LastRefreshAttemptAt);
        Assert.Null(state.LastRefreshOk);
        Assert.Null(state.LastKeepAliveAt);
        Assert.Null(state.ReauthAlertedAt);
    }

    [Fact]
    public async Task Save_ThenGet_RoundtripsAllFields()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new BrokerSessionRepository(db);

        var state = new BrokerSessionState
        {
            Status = SessionStatus.Live,
            LastSnapshotAt = T,
            LastRefreshAttemptAt = T.AddMinutes(1),
            LastRefreshOk = true,
            LastKeepAliveAt = T.AddMinutes(2),
            ReauthAlertedAt = T.AddMinutes(3),
        };

        await repo.SaveAsync(state);
        var loaded = await repo.GetAsync();

        Assert.Equal(SessionStatus.Live, loaded.Status);
        Assert.Equal(T, loaded.LastSnapshotAt);
        Assert.Equal(T.AddMinutes(1), loaded.LastRefreshAttemptAt);
        Assert.True(loaded.LastRefreshOk);
        Assert.Equal(T.AddMinutes(2), loaded.LastKeepAliveAt);
        Assert.Equal(T.AddMinutes(3), loaded.ReauthAlertedAt);
    }

    [Fact]
    public async Task Save_Upserts_SingleRowOnly()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new BrokerSessionRepository(db);

        await repo.SaveAsync(new BrokerSessionState { Status = SessionStatus.Live });
        await repo.SaveAsync(new BrokerSessionState
        {
            Status = SessionStatus.Lapsed,
            ReauthAlertedAt = T,
        });

        var loaded = await repo.GetAsync();
        Assert.Equal(SessionStatus.Lapsed, loaded.Status);
        Assert.Equal(T, loaded.ReauthAlertedAt);

        // Verify exactly one row exists (singleton).
        await using var connection = await db.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM broker_session;";
        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        Assert.Equal(1L, count);
    }

    [Fact]
    public async Task Save_AllNullableFieldsNull_Roundtrips()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new BrokerSessionRepository(db);

        await repo.SaveAsync(new BrokerSessionState { Status = SessionStatus.Unknown });

        var loaded = await repo.GetAsync();
        Assert.Equal(SessionStatus.Unknown, loaded.Status);
        Assert.Null(loaded.LastSnapshotAt);
        Assert.Null(loaded.LastRefreshAttemptAt);
        Assert.Null(loaded.LastRefreshOk);
        Assert.Null(loaded.LastKeepAliveAt);
        Assert.Null(loaded.ReauthAlertedAt);
    }

    [Fact]
    public async Task Save_LastRefreshOkFalse_Roundtrips()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new BrokerSessionRepository(db);

        await repo.SaveAsync(new BrokerSessionState
        {
            Status = SessionStatus.Live,
            LastRefreshOk = false,
        });

        var loaded = await repo.GetAsync();
        Assert.False(loaded.LastRefreshOk);
    }

    [Fact]
    public async Task Save_Null_Throws()
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new BrokerSessionRepository(db);

        await Assert.ThrowsAsync<ArgumentNullException>(() => repo.SaveAsync(null!));
    }

    [Theory]
    [InlineData(SessionStatus.Unknown)]
    [InlineData(SessionStatus.Live)]
    [InlineData(SessionStatus.Lapsed)]
    public async Task Save_AllStatusValues_Roundtrip(SessionStatus status)
    {
        await using var db = await TestDatabase.CreateAsync();
        var repo = new BrokerSessionRepository(db);

        await repo.SaveAsync(new BrokerSessionState { Status = status });

        var loaded = await repo.GetAsync();
        Assert.Equal(status, loaded.Status);
    }
}
