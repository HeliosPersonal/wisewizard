using System.Globalization;
using System.Text.Json;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;
using WiseWizard.Core.Services;

namespace WiseWizard.Infrastructure.Sec;

/// <summary>
/// SEC EDGAR filings Source (<see cref="ISecFilingsSource"/>). Fetches the company "submissions"
/// JSON for a Ticker and turns recent filings into <see cref="RawDocument"/>s. SEC grants free
/// access only to callers that declare a contact User-Agent and stay within ~10 req/s (PRD §5
/// AC-03, §6); the declared User-Agent is applied on every request and requests are paced by the
/// injected <see cref="IRateLimiter"/>. Parsing lives in the pure, fixture-tested
/// <see cref="ParseSubmissions"/>; the HTTP plumbing is thin.
/// </summary>
public sealed class EdgarFilingsSource(
    HttpClient httpClient,
    IRateLimiter rateLimiter,
    IClock clock) : ISecFilingsSource
{
    /// <summary>SEC host used for pacing (≤10 req/s).</summary>
    public const string Host = "www.sec.gov";

    private readonly HttpClient _httpClient = httpClient;
    private readonly IRateLimiter _rateLimiter = rateLimiter;
    private readonly IClock _clock = clock;

    public SourceKind Kind => SourceKind.SecFiling;

    public async Task<IReadOnlyList<RawDocument>> FetchAsync(
        Ticker ticker, long runId, DateTimeOffset since, CancellationToken ct = default)
    {
        await _rateLimiter.WaitAsync(Host, ct);

        // Source URLs are fixed/allowlisted per Source; never taken from Owner input (PRD §6.1).
        var url = $"https://{Host}/cgi-bin/browse-edgar?action=getcompany&ticker={ticker.Value}&type=&output=atom";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        // AC-03: declare a contact User-Agent on every request.
        using var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);

        return ParseSubmissions(json, ticker, runId, since, _clock.UtcNow);
    }

    /// <summary>
    /// Pure parser: SEC submissions JSON → recent-filing documents within the lookback window.
    /// The submissions shape has <c>filings.recent</c> parallel arrays (form, filingDate,
    /// primaryDocument, accessionNumber, primaryDocDescription). Malformed / missing sections
    /// yield an empty list rather than throwing.
    /// </summary>
    internal static IReadOnlyList<RawDocument> ParseSubmissions(
        string json, Ticker ticker, long runId, DateTimeOffset since, DateTimeOffset fetchedAt)
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
            if (!doc.RootElement.TryGetProperty("filings", out var filings) ||
                !filings.TryGetProperty("recent", out var recent))
            {
                return documents;
            }

            if (!recent.TryGetProperty("form", out var forms) ||
                !recent.TryGetProperty("filingDate", out var dates) ||
                !recent.TryGetProperty("accessionNumber", out var accessions) ||
                forms.ValueKind != JsonValueKind.Array)
            {
                return documents;
            }

            recent.TryGetProperty("primaryDocument", out var primaryDocs);
            recent.TryGetProperty("primaryDocDescription", out var descriptions);

            var count = forms.GetArrayLength();
            for (var i = 0; i < count; i++)
            {
                var form = ElementAt(forms, i);
                var filingDate = ElementAt(dates, i);
                var accession = ElementAt(accessions, i);
                if (form is null || filingDate is null || accession is null)
                {
                    continue;
                }

                if (!TryParseDate(filingDate, out var published) || published < since)
                {
                    continue;
                }

                var description = ElementAt(descriptions, i);
                var primaryDoc = ElementAt(primaryDocs, i);
                var accessionNoDashes = accession.Replace("-", "");
                var url = $"https://{Host}/cgi-bin/browse-edgar?action=getcompany&ticker={ticker.Value}&type={form}";
                var title = string.IsNullOrWhiteSpace(description) ? $"{form} filing" : $"{form} — {description}";
                var content = $"form={form}\naccession={accession}\nfiling_date={filingDate}\nprimary_document={primaryDoc}\naccession_key={accessionNoDashes}";

                documents.Add(new RawDocument
                {
                    DocumentId = Guid.NewGuid().ToString("N"),
                    RunId = runId,
                    Ticker = ticker,
                    Source = SourceKind.SecFiling,
                    Url = url,
                    Title = title,
                    Content = content,
                    PublishedAt = published,
                    FetchedAt = fetchedAt,
                    ContentHash = ContentHasher.Compute(title, content, url),
                });
            }
        }

        return documents;
    }

    private static string? ElementAt(JsonElement array, int index)
    {
        if (array.ValueKind != JsonValueKind.Array || index >= array.GetArrayLength())
        {
            return null;
        }

        var element = array[index];
        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }

    private static bool TryParseDate(string value, out DateTimeOffset result)
    {
        if (DateTimeOffset.TryParse(
                value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            result = parsed.ToUniversalTime();
            return true;
        }

        result = default;
        return false;
    }
}
