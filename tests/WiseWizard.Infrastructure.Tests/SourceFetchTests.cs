using System.Net;
using NSubstitute;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;
using WiseWizard.Infrastructure.Market;
using WiseWizard.Infrastructure.News;
using WiseWizard.Infrastructure.Sec;

namespace WiseWizard.Infrastructure.Tests;

public class SourceFetchTests
{
    private static readonly Ticker Aapl = Ticker.Create("AAPL");
    private static readonly DateTimeOffset FetchedAt = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Since = FetchedAt - TimeSpan.FromDays(14);

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            return Task.FromResult(response);
        }
    }

    private static (HttpClient client, StubHandler handler) HttpReturning(HttpStatusCode code, string body)
    {
        var response = new HttpResponseMessage(code) { Content = new StringContent(body) };
        var handler = new StubHandler(response);
        return (new HttpClient(handler), handler);
    }

    private static IClock Clock()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(FetchedAt);
        return clock;
    }

    private static IRateLimiter NoopLimiter() => Substitute.For<IRateLimiter>();

    [Fact]
    public void Kinds_are_correct()
    {
        var (client, _) = HttpReturning(HttpStatusCode.OK, "");
        Assert.Equal(SourceKind.SecFiling, new EdgarFilingsSource(client, NoopLimiter(), Clock()).Kind);
        Assert.Equal(SourceKind.News, new RssNewsSource(client, NoopLimiter(), Clock()).Kind);
        Assert.Equal(SourceKind.MarketData, new MarketDataSource(client, NoopLimiter(), Clock()).Kind);
    }

    [Fact]
    public async Task Sec_fetch_waits_on_limiter_and_parses()
    {
        const string body = """
            {"filings":{"recent":{
              "accessionNumber":["a"],"filingDate":["2026-07-20"],"form":["8-K"],
              "primaryDocument":["x"],"primaryDocDescription":["d"]
            }}}
            """;
        var (client, _) = HttpReturning(HttpStatusCode.OK, body);
        var limiter = NoopLimiter();
        var source = new EdgarFilingsSource(client, limiter, Clock());

        var docs = await source.FetchAsync(Aapl, 1, Since);

        Assert.Single(docs);
        await limiter.Received(1).WaitAsync(EdgarFilingsSource.Host, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sec_fetch_throws_on_http_error()
    {
        var (client, _) = HttpReturning(HttpStatusCode.TooManyRequests, "rate limited");
        var source = new EdgarFilingsSource(client, NoopLimiter(), Clock());

        await Assert.ThrowsAsync<HttpRequestException>(() => source.FetchAsync(Aapl, 1, Since));
    }

    [Fact]
    public async Task Rss_fetch_waits_on_limiter_and_parses()
    {
        const string body = """
            <rss><channel><item>
              <title>T</title><link>l</link><description>d</description>
              <pubDate>Mon, 20 Jul 2026 09:00:00 GMT</pubDate>
            </item></channel></rss>
            """;
        var (client, _) = HttpReturning(HttpStatusCode.OK, body);
        var limiter = NoopLimiter();
        var source = new RssNewsSource(client, limiter, Clock());

        var docs = await source.FetchAsync(Aapl, 1, Since);

        Assert.Single(docs);
        await limiter.Received(1).WaitAsync(RssNewsSource.Host, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rss_fetch_throws_on_http_error()
    {
        var (client, _) = HttpReturning(HttpStatusCode.ServiceUnavailable, "down");
        var source = new RssNewsSource(client, NoopLimiter(), Clock());

        await Assert.ThrowsAsync<HttpRequestException>(() => source.FetchAsync(Aapl, 1, Since));
    }

    [Fact]
    public async Task Market_fetch_waits_on_limiter_and_parses()
    {
        const string body = """{"chart":{"result":[{"meta":{"regularMarketPrice":10.0}}]}}""";
        var (client, _) = HttpReturning(HttpStatusCode.OK, body);
        var limiter = NoopLimiter();
        var source = new MarketDataSource(client, limiter, Clock());

        var docs = await source.FetchAsync(Aapl, 1, Since);

        Assert.Single(docs);
        await limiter.Received(1).WaitAsync(MarketDataSource.Host, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Market_fetch_throws_on_http_error()
    {
        var (client, _) = HttpReturning(HttpStatusCode.InternalServerError, "boom");
        var source = new MarketDataSource(client, NoopLimiter(), Clock());

        await Assert.ThrowsAsync<HttpRequestException>(() => source.FetchAsync(Aapl, 1, Since));
    }
}
