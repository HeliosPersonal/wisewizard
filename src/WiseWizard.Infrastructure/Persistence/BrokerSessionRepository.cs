using System.Globalization;
using Dapper;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;

namespace WiseWizard.Infrastructure.Persistence;

/// <summary>
/// Dapper/PostgreSQL persistence for the singleton <c>broker_session</c> row (<c>id = 1</c>).
/// <see cref="GetAsync"/> returns an <see cref="SessionStatus.Unknown"/> default when the row is
/// absent; <see cref="SaveAsync"/> upserts. <see cref="SessionStatus"/> maps to/from
/// <c>live</c>/<c>lapsed</c>/<c>unknown</c> text and <see cref="bool"/>? to/from
/// <c>'true'</c>/<c>'false'</c>/null.
/// </summary>
public sealed class BrokerSessionRepository(IDbConnectionFactory factory) : IBrokerSessionRepository
{
    private readonly IDbConnectionFactory _factory = factory;

    public async Task<BrokerSessionState> GetAsync(CancellationToken ct = default)
    {
        await using var connection = await _factory.OpenAsync(ct);

        var row = await connection.QuerySingleOrDefaultAsync<SessionRow>(new CommandDefinition(
            """
            SELECT status, last_snapshot_at, last_refresh_attempt_at, last_refresh_ok,
                   last_keepalive_at, reauth_alerted_at
            FROM broker_session WHERE id = 1;
            """,
            cancellationToken: ct));

        if (row is null)
        {
            return new BrokerSessionState { Status = SessionStatus.Unknown };
        }

        return new BrokerSessionState
        {
            Status = ParseStatus(row.status),
            LastSnapshotAt = ParseInstant(row.last_snapshot_at),
            LastRefreshAttemptAt = ParseInstant(row.last_refresh_attempt_at),
            LastRefreshOk = ParseBool(row.last_refresh_ok),
            LastKeepAliveAt = ParseInstant(row.last_keepalive_at),
            ReauthAlertedAt = ParseInstant(row.reauth_alerted_at),
        };
    }

    public async Task SaveAsync(BrokerSessionState state, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        await using var connection = await _factory.OpenAsync(ct);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO broker_session
                (id, status, last_snapshot_at, last_refresh_attempt_at, last_refresh_ok,
                 last_keepalive_at, reauth_alerted_at)
            VALUES
                (1, @Status, @LastSnapshotAt, @LastRefreshAttemptAt, @LastRefreshOk,
                 @LastKeepAliveAt, @ReauthAlertedAt)
            ON CONFLICT(id) DO UPDATE SET
                status = excluded.status,
                last_snapshot_at = excluded.last_snapshot_at,
                last_refresh_attempt_at = excluded.last_refresh_attempt_at,
                last_refresh_ok = excluded.last_refresh_ok,
                last_keepalive_at = excluded.last_keepalive_at,
                reauth_alerted_at = excluded.reauth_alerted_at;
            """,
            new
            {
                Status = FormatStatus(state.Status),
                LastSnapshotAt = FormatInstant(state.LastSnapshotAt),
                LastRefreshAttemptAt = FormatInstant(state.LastRefreshAttemptAt),
                LastRefreshOk = FormatBool(state.LastRefreshOk),
                LastKeepAliveAt = FormatInstant(state.LastKeepAliveAt),
                ReauthAlertedAt = FormatInstant(state.ReauthAlertedAt),
            },
            cancellationToken: ct));
    }

    private static string FormatStatus(SessionStatus status) => status switch
    {
        SessionStatus.Live => "live",
        SessionStatus.Lapsed => "lapsed",
        _ => "unknown",
    };

    private static SessionStatus ParseStatus(string? text) => text switch
    {
        "live" => SessionStatus.Live,
        "lapsed" => SessionStatus.Lapsed,
        _ => SessionStatus.Unknown,
    };

    private static string? FormatBool(bool? value) => value switch
    {
        true => "true",
        false => "false",
        null => null,
    };

    private static bool? ParseBool(string? text) => text switch
    {
        "true" => true,
        "false" => false,
        _ => null,
    };

    private static string? FormatInstant(DateTimeOffset? instant) =>
        instant?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset? ParseInstant(string? text) =>
        string.IsNullOrEmpty(text)
            ? null
            : DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private sealed record SessionRow
    {
        public string status { get; init; } = "unknown";
        public string? last_snapshot_at { get; init; }
        public string? last_refresh_attempt_at { get; init; }
        public string? last_refresh_ok { get; init; }
        public string? last_keepalive_at { get; init; }
        public string? reauth_alerted_at { get; init; }
    }
}
