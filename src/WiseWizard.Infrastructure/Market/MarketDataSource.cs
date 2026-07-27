using System.Text.Json;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;
using WiseWizard.Core.Services;

namespace WiseWizard.Infrastructure.Market;

/// <summary>
/// Market / fundamental data Source (<see cref="IMarketDataSource"/>). Fetches a latest metrics
/// snapshot for a Ticker and turns it into a single <see cref="RawDocument"/> (market data is the
/// latest snapshot, not a lookback list — PRD §6). The host is paced at ≤1 req/s/host (PRD §6) via
/// the injected <see cref="IRateLimiter"/>. Parsing lives in the pure, fixture-tested
/// <see cref="ParseSnapshot"/>; the HTTP plumbing is thin.
/// </summary>
public sealed class MarketDataSource(
    HttpClient httpClient,
    IRateLimiter rateLimiter,
    IClock clock) : IMarketDataSource
{
    /// <summary>The market-data host used for pacing (≤1 req/s/host).</summary>
    public const string Host = "query1.finance.yahoo.com";

    private readonly HttpClient _httpClient = httpClient;
    private readonly IRateLimiter _rateLimiter = rateLimiter;
    private readonly IClock _clock = clock;

    public SourceKind Kind => SourceKind.MarketData;

    public async Task<IReadOnlyList<RawDocument>> FetchAsync(
        Ticker ticker, long runId, DateTimeOffset since, CancellationToken ct = default)
    {
        await _rateLimiter.WaitAsync(Host, ct);

        // Source URLs are fixed/allowlisted per Source; never taken from Owner input (PRD §6.1).
        var url = $"https://{Host}/v8/finance/chart/{ticker.Value}";
        using var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);

        return ParseSnapshot(json, ticker, runId, url, _clock.UtcNow);
    }

    /// <summary>
    /// Pure parser: market-data JSON → a single metrics-snapshot document. Reads the Yahoo-style
    /// <c>chart.result[0].meta</c> block (regularMarketPrice, previousClose, currency,
    /// regularMarketTime). The snapshot is timestamped at fetch time (latest snapshot, not
    /// lookback). Malformed JSON or a missing meta block yields an empty list rather than throwing.
    /// </summary>
    internal static IReadOnlyList<RawDocument> ParseSnapshot(
        string json, Ticker ticker, long runId, string url, DateTimeOffset fetchedAt)
    {
        var documents = new List<RawDocument>();

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return documents;
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("chart", out var chart) ||
                !chart.TryGetProperty("result", out var result) ||
                result.ValueKind != JsonValueKind.Array ||
                result.GetArrayLength() == 0)
            {
                return documents;
            }

            var first = result[0];
            if (!first.TryGetProperty("meta", out var meta) ||
                meta.ValueKind != JsonValueKind.Object)
            {
                return documents;
            }

            var price = GetNumber(meta, "regularMarketPrice");
            var previousClose = GetNumber(meta, "previousClose") ?? GetNumber(meta, "chartPreviousClose");
            var currency = GetString(meta, "currency") ?? "USD";

            // A snapshot with no price at all carries no useful metrics — treat as empty.
            if (price is null && previousClose is null)
            {
                return documents;
            }

            var content =
                $"ticker={ticker.Value}\n" +
                $"currency={currency}\n" +
                $"regular_market_price={Format(price)}\n" +
                $"previous_close={Format(previousClose)}";
            var title = $"{ticker.Value} market snapshot";

            documents.Add(new RawDocument
            {
                DocumentId = Guid.NewGuid().ToString("N"),
                RunId = runId,
                Ticker = ticker,
                Source = SourceKind.MarketData,
                Url = url,
                Title = title,
                Content = content,
                PublishedAt = fetchedAt,
                FetchedAt = fetchedAt,
                // Hash the content only (excluding fetch-time) so an unchanged snapshot dedupes
                // within a Run.
                ContentHash = ContentHasher.Compute(title, content),
            });
        }

        return documents;
    }

    private static double? GetNumber(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;

    private static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string Format(double? value) =>
        value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "n/a";
}
