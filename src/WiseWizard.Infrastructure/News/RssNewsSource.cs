using System.Globalization;
using System.Xml.Linq;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;
using WiseWizard.Core.Services;

namespace WiseWizard.Infrastructure.News;

/// <summary>
/// News RSS Source (<see cref="INewsSource"/>). Fetches an RSS feed scoped to a Ticker symbol and
/// turns its items into <see cref="RawDocument"/>s. RSS hosts are paced at ≤1 req/s/host (PRD §6)
/// via the injected <see cref="IRateLimiter"/>. Parsing lives in the pure, fixture-tested
/// <see cref="ParseRss"/> using <c>System.Xml.Linq</c> (no extra deps); the HTTP plumbing is thin.
/// </summary>
public sealed class RssNewsSource(
    HttpClient httpClient,
    IRateLimiter rateLimiter,
    IClock clock) : INewsSource
{
    /// <summary>The RSS host used for pacing (≤1 req/s/host).</summary>
    public const string Host = "news.google.com";

    private readonly HttpClient _httpClient = httpClient;
    private readonly IRateLimiter _rateLimiter = rateLimiter;
    private readonly IClock _clock = clock;

    public SourceKind Kind => SourceKind.News;

    public async Task<IReadOnlyList<RawDocument>> FetchAsync(
        Ticker ticker, long runId, DateTimeOffset since, CancellationToken ct = default)
    {
        await _rateLimiter.WaitAsync(Host, ct);

        // Source URLs are fixed/allowlisted per Source; never taken from Owner input (PRD §6.1).
        var url = $"https://{Host}/rss/search?q={ticker.Value}+stock";
        using var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var xml = await response.Content.ReadAsStringAsync(ct);

        return ParseRss(xml, ticker, runId, since, _clock.UtcNow);
    }

    /// <summary>
    /// Pure parser: RSS XML → article documents within the lookback window. Reads each
    /// <c>&lt;item&gt;</c>'s title, link, description, and pubDate. Items without a parseable
    /// pubDate are kept (published_at null) so freshly seen but undated news is not dropped.
    /// Malformed XML yields an empty list rather than throwing.
    /// </summary>
    internal static IReadOnlyList<RawDocument> ParseRss(
        string xml, Ticker ticker, long runId, DateTimeOffset since, DateTimeOffset fetchedAt)
    {
        var documents = new List<RawDocument>();

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return documents;
        }

        // A successfully parsed XDocument always has a Root element.
        var channel = doc.Root!.Element("channel");
        if (channel is null)
        {
            return documents;
        }

        foreach (var item in channel.Elements("item"))
        {
            var title = (string?)item.Element("title") ?? string.Empty;
            var link = (string?)item.Element("link");
            var description = (string?)item.Element("description") ?? string.Empty;
            var pubDateRaw = (string?)item.Element("pubDate");

            DateTimeOffset? published = null;
            if (!string.IsNullOrWhiteSpace(pubDateRaw) &&
                DateTimeOffset.TryParse(
                    pubDateRaw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            {
                var utc = parsed.ToUniversalTime();
                if (utc < since)
                {
                    // Older than the lookback window (AC-06) — discard.
                    continue;
                }

                published = utc;
            }

            documents.Add(new RawDocument
            {
                DocumentId = Guid.NewGuid().ToString("N"),
                RunId = runId,
                Ticker = ticker,
                Source = SourceKind.News,
                Url = link,
                Title = title,
                Content = description,
                PublishedAt = published,
                FetchedAt = fetchedAt,
                ContentHash = ContentHasher.Compute(title, description, link),
            });
        }

        return documents;
    }
}
