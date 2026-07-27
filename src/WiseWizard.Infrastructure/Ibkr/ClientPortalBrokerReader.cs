using System.Text.Json;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;

namespace WiseWizard.Infrastructure.Ibkr;

/// <summary>
/// Read-only <see cref="IBrokerReader"/> over the IBKR Client Portal local REST gateway
/// (bound to localhost). Exposes NO order-placing capability — the read-only invariant is
/// enforced at the type level (PRD §AC-05, ADR-0002).
///
/// HTTP plumbing is kept deliberately thin; the gateway-JSON → Core <see cref="Position"/> mapping
/// lives in <see cref="MapPositions"/>, a pure static method unit-tested against saved fixtures.
/// </summary>
public sealed class ClientPortalBrokerReader(HttpClient http, string accountId) : IBrokerReader
{
    // Gateway endpoints (read-only). tickle keeps the session alive; iserver/auth/status reports it.
    private const string PositionsPath = "v1/api/portfolio/{0}/positions/0";
    private const string AuthStatusPath = "v1/api/iserver/auth/status";
    private const string TicklePath = "v1/api/tickle";

    private readonly HttpClient _http = http;
    private readonly string _accountId = accountId;

    public async Task<IReadOnlyList<Position>> ReadPositionsAsync(CancellationToken ct = default)
    {
        var path = string.Format(System.Globalization.CultureInfo.InvariantCulture, PositionsPath, _accountId);
        using var response = await _http.GetAsync(path, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        return MapPositions(json, DateTimeOffset.UtcNow);
    }

    public async Task<bool> IsSessionLiveAsync(CancellationToken ct = default)
    {
        using var response = await _http.PostAsync(AuthStatusPath, content: null, ct);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseAuthenticated(json);
    }

    public async Task<bool> KeepAliveAsync(CancellationToken ct = default)
    {
        using var response = await _http.PostAsync(TicklePath, content: null, ct);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseTickleAuthenticated(json);
    }

    /// <summary>
    /// Maps a Client Portal positions-endpoint JSON array to Core <see cref="Position"/> records,
    /// stamping every row with the shared snapshot <paramref name="asOf"/>. Pure and side-effect free.
    /// </summary>
    internal static IReadOnlyList<Position> MapPositions(string json, DateTimeOffset asOf)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<Position>();
        foreach (var element in root.EnumerateArray())
        {
            var symbol = ReadSymbol(element);
            if (symbol is null)
            {
                continue;
            }

            result.Add(new Position
            {
                Ticker = Ticker.Create(symbol),
                Quantity = ReadDecimal(element, "position"),
                AvgCost = ReadDecimal(element, "avgCost"),
                MarketValue = ReadDecimal(element, "mktValue"),
                UnrealizedPnl = ReadDecimal(element, "unrealizedPnl"),
                Currency = ReadString(element, "currency") ?? "USD",
                AsOf = asOf,
            });
        }

        return result;
    }

    private static string? ReadSymbol(JsonElement element)
    {
        // Prefer the plain ticker; fall back to the contract description's leading token.
        var ticker = ReadString(element, "ticker");
        if (!string.IsNullOrWhiteSpace(ticker))
        {
            return ticker;
        }

        var desc = ReadString(element, "contractDesc");
        if (string.IsNullOrWhiteSpace(desc))
        {
            return null;
        }

        // desc is non-whitespace here, so RemoveEmptyEntries always yields at least one token.
        return desc.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static decimal ReadDecimal(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var prop))
        {
            return 0m;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.Number => prop.GetDecimal(),
            JsonValueKind.String when decimal.TryParse(
                prop.GetString(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => 0m,
        };
    }

    internal static bool ParseAuthenticated(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.ValueKind == JsonValueKind.Object
            && doc.RootElement.TryGetProperty("authenticated", out var auth)
            && auth.ValueKind == JsonValueKind.True;
    }

    internal static bool ParseTickleAuthenticated(string json)
    {
        using var doc = JsonDocument.Parse(json);
        // tickle returns { "iserver": { "authStatus": { "authenticated": true } } }
        return doc.RootElement.ValueKind == JsonValueKind.Object
            && doc.RootElement.TryGetProperty("iserver", out var iserver)
            && iserver.ValueKind == JsonValueKind.Object
            && iserver.TryGetProperty("authStatus", out var authStatus)
            && authStatus.ValueKind == JsonValueKind.Object
            && authStatus.TryGetProperty("authenticated", out var auth)
            && auth.ValueKind == JsonValueKind.True;
    }
}
