using WiseWizard.Core.Models;
using WiseWizard.Infrastructure.Market;
using WiseWizard.Infrastructure.News;
using WiseWizard.Infrastructure.Sec;

namespace WiseWizard.Infrastructure.Tests;

public class SourceParsingTests
{
    private static readonly Ticker Aapl = Ticker.Create("AAPL");
    private static readonly DateTimeOffset FetchedAt = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Since = FetchedAt - TimeSpan.FromDays(14);

    // ---- SEC EDGAR ----

    private const string SecFixture = """
        {
          "cik": "320193",
          "filings": {
            "recent": {
              "accessionNumber": ["0000320193-26-000010", "0000320193-26-000009", "0000320193-24-000001"],
              "filingDate": ["2026-07-20", "2026-07-18", "2024-01-05"],
              "form": ["8-K", "10-Q", "10-K"],
              "primaryDocument": ["aapl-8k.htm", "aapl-10q.htm", "aapl-10k.htm"],
              "primaryDocDescription": ["Current report", "Quarterly report", "Annual report"]
            }
          }
        }
        """;

    [Fact]
    public void Sec_parses_recent_filings_within_lookback()
    {
        var docs = EdgarFilingsSource.ParseSubmissions(SecFixture, Aapl, 1, Since, FetchedAt);

        // 8-K and 10-Q are within 14 days; 10-K from 2024 is outside the window.
        Assert.Equal(2, docs.Count);
        Assert.All(docs, d =>
        {
            Assert.Equal(SourceKind.SecFiling, d.Source);
            Assert.Equal(Aapl, d.Ticker);
            Assert.Equal(1, d.RunId);
            Assert.Equal(FetchedAt, d.FetchedAt);
            Assert.NotNull(d.PublishedAt);
            Assert.True(d.PublishedAt >= Since);
            Assert.NotEmpty(d.ContentHash);
        });
        Assert.Contains(docs, d => d.Title.Contains("8-K"));
        Assert.Contains(docs, d => d.Title.Contains("10-Q"));
        Assert.DoesNotContain(docs, d => d.Title.Contains("10-K"));
    }

    [Fact]
    public void Sec_filing_without_description_uses_fallback_title()
    {
        const string fixture = """
            {"filings":{"recent":{
              "accessionNumber":["0000320193-26-000010"],
              "filingDate":["2026-07-20"],
              "form":["4"],
              "primaryDocument":["form4.xml"],
              "primaryDocDescription":[""]
            }}}
            """;

        var doc = Assert.Single(EdgarFilingsSource.ParseSubmissions(fixture, Aapl, 1, Since, FetchedAt));
        Assert.Equal("4 filing", doc.Title);
    }

    [Fact]
    public void Sec_malformed_json_returns_empty()
    {
        Assert.Empty(EdgarFilingsSource.ParseSubmissions("{not valid", Aapl, 1, Since, FetchedAt));
    }

    [Fact]
    public void Sec_missing_filings_section_returns_empty()
    {
        Assert.Empty(EdgarFilingsSource.ParseSubmissions("""{"cik":"1"}""", Aapl, 1, Since, FetchedAt));
        Assert.Empty(EdgarFilingsSource.ParseSubmissions("""{"filings":{}}""", Aapl, 1, Since, FetchedAt));
    }

    [Fact]
    public void Sec_missing_parallel_arrays_returns_empty()
    {
        // form present but filingDate missing.
        const string fixture = """{"filings":{"recent":{"form":["8-K"]}}}""";
        Assert.Empty(EdgarFilingsSource.ParseSubmissions(fixture, Aapl, 1, Since, FetchedAt));
    }

    [Fact]
    public void Sec_skips_entries_with_unparseable_date()
    {
        const string fixture = """
            {"filings":{"recent":{
              "accessionNumber":["a","b"],
              "filingDate":["not-a-date","2026-07-20"],
              "form":["8-K","10-Q"],
              "primaryDocument":["x","y"],
              "primaryDocDescription":["d1","d2"]
            }}}
            """;

        var doc = Assert.Single(EdgarFilingsSource.ParseSubmissions(fixture, Aapl, 1, Since, FetchedAt));
        Assert.Contains("10-Q", doc.Title);
    }

    [Fact]
    public void Sec_handles_shorter_optional_arrays_gracefully()
    {
        // primaryDocument / primaryDocDescription shorter than form -> ElementAt out-of-range path.
        const string fixture = """
            {"filings":{"recent":{
              "accessionNumber":["a"],
              "filingDate":["2026-07-20"],
              "form":["8-K"],
              "primaryDocument":[],
              "primaryDocDescription":[]
            }}}
            """;

        var doc = Assert.Single(EdgarFilingsSource.ParseSubmissions(fixture, Aapl, 1, Since, FetchedAt));
        Assert.Equal("8-K filing", doc.Title);
    }

    [Fact]
    public void Sec_handles_absent_optional_arrays()
    {
        // primaryDocument / primaryDocDescription entirely absent -> non-array (Undefined) path.
        const string fixture = """
            {"filings":{"recent":{
              "accessionNumber":["a"],
              "filingDate":["2026-07-20"],
              "form":["8-K"]
            }}}
            """;

        var doc = Assert.Single(EdgarFilingsSource.ParseSubmissions(fixture, Aapl, 1, Since, FetchedAt));
        Assert.Equal("8-K filing", doc.Title);
    }

    [Fact]
    public void Sec_skips_entries_with_null_form_or_accession()
    {
        const string fixture = """
            {"filings":{"recent":{
              "accessionNumber":[null,"b"],
              "filingDate":["2026-07-20","2026-07-19"],
              "form":["8-K",null],
              "primaryDocument":["x","y"],
              "primaryDocDescription":["d1","d2"]
            }}}
            """;

        Assert.Empty(EdgarFilingsSource.ParseSubmissions(fixture, Aapl, 1, Since, FetchedAt));
    }

    // ---- RSS News ----

    private const string RssFixture = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0">
          <channel>
            <title>AAPL news</title>
            <item>
              <title>Apple beats earnings</title>
              <link>https://news.example/aapl-earnings</link>
              <description>Apple reported strong results.</description>
              <pubDate>Mon, 20 Jul 2026 09:00:00 GMT</pubDate>
            </item>
            <item>
              <title>Old Apple story</title>
              <link>https://news.example/old</link>
              <description>Ancient news.</description>
              <pubDate>Mon, 01 Jan 2024 09:00:00 GMT</pubDate>
            </item>
            <item>
              <title>Undated Apple note</title>
              <link>https://news.example/undated</link>
              <description>No date here.</description>
            </item>
          </channel>
        </rss>
        """;

    [Fact]
    public void Rss_parses_items_within_lookback_and_keeps_undated()
    {
        var docs = RssNewsSource.ParseRss(RssFixture, Aapl, 5, Since, FetchedAt);

        // fresh + undated kept; the 2024 item is discarded.
        Assert.Equal(2, docs.Count);
        Assert.All(docs, d =>
        {
            Assert.Equal(SourceKind.News, d.Source);
            Assert.Equal(5, d.RunId);
            Assert.Equal(FetchedAt, d.FetchedAt);
            Assert.NotEmpty(d.ContentHash);
        });

        var fresh = docs.Single(d => d.Title == "Apple beats earnings");
        Assert.Equal("https://news.example/aapl-earnings", fresh.Url);
        Assert.Equal("Apple reported strong results.", fresh.Content);
        Assert.NotNull(fresh.PublishedAt);

        var undated = docs.Single(d => d.Title == "Undated Apple note");
        Assert.Null(undated.PublishedAt);
    }

    [Fact]
    public void Rss_malformed_xml_returns_empty()
    {
        Assert.Empty(RssNewsSource.ParseRss("<rss><channel", Aapl, 1, Since, FetchedAt));
    }

    [Fact]
    public void Rss_missing_channel_returns_empty()
    {
        Assert.Empty(RssNewsSource.ParseRss("<rss></rss>", Aapl, 1, Since, FetchedAt));
    }

    [Fact]
    public void Rss_item_with_missing_optional_fields_still_parses()
    {
        const string fixture = """
            <rss><channel><item><pubDate>Mon, 20 Jul 2026 09:00:00 GMT</pubDate></item></channel></rss>
            """;

        var doc = Assert.Single(RssNewsSource.ParseRss(fixture, Aapl, 1, Since, FetchedAt));
        Assert.Equal(string.Empty, doc.Title);
        Assert.Null(doc.Url);
        Assert.Equal(string.Empty, doc.Content);
    }

    [Fact]
    public void Rss_item_with_unparseable_date_is_treated_as_undated()
    {
        const string fixture = """
            <rss><channel><item>
              <title>T</title><link>l</link><description>d</description>
              <pubDate>garbage</pubDate>
            </item></channel></rss>
            """;

        var doc = Assert.Single(RssNewsSource.ParseRss(fixture, Aapl, 1, Since, FetchedAt));
        Assert.Null(doc.PublishedAt);
    }

    // ---- Market data ----

    private const string MarketFixture = """
        {
          "chart": {
            "result": [
              {
                "meta": {
                  "currency": "USD",
                  "symbol": "AAPL",
                  "regularMarketPrice": 213.45,
                  "previousClose": 210.10,
                  "regularMarketTime": 1785000000
                }
              }
            ],
            "error": null
          }
        }
        """;

    [Fact]
    public void Market_parses_snapshot()
    {
        var doc = Assert.Single(
            MarketDataSource.ParseSnapshot(MarketFixture, Aapl, 9, "https://mkt/aapl", FetchedAt));

        Assert.Equal(SourceKind.MarketData, doc.Source);
        Assert.Equal(9, doc.RunId);
        Assert.Equal("https://mkt/aapl", doc.Url);
        Assert.Equal("AAPL market snapshot", doc.Title);
        Assert.Equal(FetchedAt, doc.FetchedAt);
        Assert.Equal(FetchedAt, doc.PublishedAt);
        Assert.Contains("regular_market_price=213.45", doc.Content);
        Assert.Contains("previous_close=210.1", doc.Content);
        Assert.Contains("currency=USD", doc.Content);
        Assert.NotEmpty(doc.ContentHash);
    }

    [Fact]
    public void Market_uses_chartPreviousClose_and_default_currency_fallbacks()
    {
        const string fixture = """
            {"chart":{"result":[{"meta":{"chartPreviousClose":100.5}}]}}
            """;

        var doc = Assert.Single(MarketDataSource.ParseSnapshot(fixture, Aapl, 1, "u", FetchedAt));
        Assert.Contains("previous_close=100.5", doc.Content);
        Assert.Contains("regular_market_price=n/a", doc.Content);
        Assert.Contains("currency=USD", doc.Content);
    }

    [Fact]
    public void Market_malformed_json_returns_empty()
    {
        Assert.Empty(MarketDataSource.ParseSnapshot("{bad", Aapl, 1, "u", FetchedAt));
    }

    [Fact]
    public void Market_missing_chart_or_result_returns_empty()
    {
        Assert.Empty(MarketDataSource.ParseSnapshot("""{"chart":{"result":[]}}""", Aapl, 1, "u", FetchedAt));
        Assert.Empty(MarketDataSource.ParseSnapshot("""{"chart":{}}""", Aapl, 1, "u", FetchedAt));
        Assert.Empty(MarketDataSource.ParseSnapshot("""{}""", Aapl, 1, "u", FetchedAt));
    }

    [Fact]
    public void Market_missing_meta_returns_empty()
    {
        Assert.Empty(MarketDataSource.ParseSnapshot("""{"chart":{"result":[{}]}}""", Aapl, 1, "u", FetchedAt));
    }

    [Fact]
    public void Market_snapshot_without_any_price_returns_empty()
    {
        const string fixture = """{"chart":{"result":[{"meta":{"currency":"USD"}}]}}""";
        Assert.Empty(MarketDataSource.ParseSnapshot(fixture, Aapl, 1, "u", FetchedAt));
    }
}
