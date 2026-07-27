using System.Net;
using System.Reflection;
using System.Text;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Models;
using WiseWizard.Infrastructure.Ibkr;

namespace WiseWizard.Infrastructure.Tests;

public sealed class ClientPortalBrokerReaderTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 7, 26, 3, 0, 0, TimeSpan.Zero);

    private const string MultiPositionJson = """
        [
          { "ticker": "AAPL", "position": 12.5, "avgCost": 149.99, "mktValue": 2100.75, "unrealizedPnl": 350.25, "currency": "USD" },
          { "ticker": "SHOP", "position": 3, "avgCost": 80.0, "mktValue": 300.0, "unrealizedPnl": -60.0, "currency": "CAD" }
        ]
        """;

    private const string OnePositionJson = """
        [
          { "ticker": "MSFT", "position": 10, "avgCost": 250.0, "mktValue": 2800.0, "unrealizedPnl": 300.0, "currency": "USD" }
        ]
        """;

    private const string EmptyJson = "[]";

    [Fact]
    public void MapPositions_MultiPosition_MapsEveryField()
    {
        var positions = ClientPortalBrokerReader.MapPositions(MultiPositionJson, AsOf);

        Assert.Equal(2, positions.Count);

        var aapl = positions[0];
        Assert.Equal(Ticker.Create("AAPL"), aapl.Ticker);
        Assert.Equal(12.5m, aapl.Quantity);
        Assert.Equal(149.99m, aapl.AvgCost);
        Assert.Equal(2100.75m, aapl.MarketValue);
        Assert.Equal(350.25m, aapl.UnrealizedPnl);
        Assert.Equal("USD", aapl.Currency);
        Assert.Equal(AsOf, aapl.AsOf);

        var shop = positions[1];
        Assert.Equal(Ticker.Create("SHOP"), shop.Ticker);
        Assert.Equal(-60.0m, shop.UnrealizedPnl);
        Assert.Equal("CAD", shop.Currency);
    }

    [Fact]
    public void MapPositions_OnePosition_MapsSingleRow()
    {
        var positions = ClientPortalBrokerReader.MapPositions(OnePositionJson, AsOf);

        var msft = Assert.Single(positions);
        Assert.Equal(Ticker.Create("MSFT"), msft.Ticker);
        Assert.Equal(10m, msft.Quantity);
    }

    [Fact]
    public void MapPositions_Empty_ReturnsEmpty()
    {
        var positions = ClientPortalBrokerReader.MapPositions(EmptyJson, AsOf);

        Assert.Empty(positions);
    }

    [Fact]
    public void MapPositions_NonArrayRoot_ReturnsEmpty()
    {
        var positions = ClientPortalBrokerReader.MapPositions("""{ "error": "no data" }""", AsOf);

        Assert.Empty(positions);
    }

    [Fact]
    public void MapPositions_FallsBackToContractDescForSymbol()
    {
        const string json = """
            [ { "contractDesc": "NVDA NASDAQ STK", "position": 4, "avgCost": 100, "mktValue": 500, "unrealizedPnl": 100 } ]
            """;

        var positions = ClientPortalBrokerReader.MapPositions(json, AsOf);

        var nvda = Assert.Single(positions);
        Assert.Equal(Ticker.Create("NVDA"), nvda.Ticker);
        Assert.Equal("USD", nvda.Currency);
    }

    [Fact]
    public void MapPositions_SkipsRowsWithNoSymbol()
    {
        const string json = """
            [
              { "position": 4, "avgCost": 100, "mktValue": 500, "unrealizedPnl": 100 },
              { "contractDesc": "   ", "position": 1 },
              { "ticker": "AAPL", "position": 1, "avgCost": 1, "mktValue": 1, "unrealizedPnl": 0 }
            ]
            """;

        var positions = ClientPortalBrokerReader.MapPositions(json, AsOf);

        var only = Assert.Single(positions);
        Assert.Equal(Ticker.Create("AAPL"), only.Ticker);
    }

    [Fact]
    public void MapPositions_NumericAsString_IsParsed()
    {
        const string json = """
            [ { "ticker": "AAPL", "position": "7.5", "avgCost": "100.25", "mktValue": "800", "unrealizedPnl": "50" } ]
            """;

        var only = Assert.Single(ClientPortalBrokerReader.MapPositions(json, AsOf));
        Assert.Equal(7.5m, only.Quantity);
        Assert.Equal(100.25m, only.AvgCost);
    }

    [Fact]
    public void MapPositions_MissingOrUnparseableNumbers_DefaultToZero()
    {
        const string json = """
            [ { "ticker": "AAPL", "avgCost": "not-a-number" } ]
            """;

        var only = Assert.Single(ClientPortalBrokerReader.MapPositions(json, AsOf));
        Assert.Equal(0m, only.Quantity);
        Assert.Equal(0m, only.AvgCost);
        Assert.Equal(0m, only.MarketValue);
        Assert.Equal(0m, only.UnrealizedPnl);
    }

    [Theory]
    [InlineData("""{ "authenticated": true }""", true)]
    [InlineData("""{ "authenticated": false }""", false)]
    [InlineData("""{ "other": 1 }""", false)]
    [InlineData("[]", false)]
    public void ParseAuthenticated_ReadsAuthenticatedFlag(string json, bool expected)
    {
        Assert.Equal(expected, ClientPortalBrokerReader.ParseAuthenticated(json));
    }

    [Theory]
    [InlineData("""{ "iserver": { "authStatus": { "authenticated": true } } }""", true)]
    [InlineData("""{ "iserver": { "authStatus": { "authenticated": false } } }""", false)]
    [InlineData("""{ "iserver": { } }""", false)]
    [InlineData("""{ "iserver": "x" }""", false)]
    [InlineData("""{ }""", false)]
    [InlineData("[]", false)]
    public void ParseTickleAuthenticated_ReadsNestedAuthStatus(string json, bool expected)
    {
        Assert.Equal(expected, ClientPortalBrokerReader.ParseTickleAuthenticated(json));
    }

    [Fact]
    public async Task ReadPositionsAsync_HitsPositionsEndpointAndMaps()
    {
        var handler = new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(OnePositionJson, Encoding.UTF8, "application/json") });
        var reader = new ClientPortalBrokerReader(NewClient(handler), "U123456");

        var positions = await reader.ReadPositionsAsync();

        var only = Assert.Single(positions);
        Assert.Equal(Ticker.Create("MSFT"), only.Ticker);
        Assert.Contains("portfolio/U123456/positions/0", handler.LastRequestUri!.ToString());
    }

    [Fact]
    public async Task ReadPositionsAsync_NonSuccess_Throws()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var reader = new ClientPortalBrokerReader(NewClient(handler), "U1");

        await Assert.ThrowsAsync<HttpRequestException>(() => reader.ReadPositionsAsync());
    }

    [Fact]
    public async Task IsSessionLiveAsync_Authenticated_ReturnsTrue()
    {
        var handler = new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{ "authenticated": true }""") });
        var reader = new ClientPortalBrokerReader(NewClient(handler), "U1");

        Assert.True(await reader.IsSessionLiveAsync());
        Assert.Contains("iserver/auth/status", handler.LastRequestUri!.ToString());
    }

    [Fact]
    public async Task IsSessionLiveAsync_NonSuccess_ReturnsFalse()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var reader = new ClientPortalBrokerReader(NewClient(handler), "U1");

        Assert.False(await reader.IsSessionLiveAsync());
    }

    [Fact]
    public async Task KeepAliveAsync_Authenticated_ReturnsTrue()
    {
        var handler = new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "iserver": { "authStatus": { "authenticated": true } } }"""),
            });
        var reader = new ClientPortalBrokerReader(NewClient(handler), "U1");

        Assert.True(await reader.KeepAliveAsync());
        Assert.Contains("tickle", handler.LastRequestUri!.ToString());
    }

    [Fact]
    public async Task KeepAliveAsync_NonSuccess_ReturnsFalse()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var reader = new ClientPortalBrokerReader(NewClient(handler), "U1");

        Assert.False(await reader.KeepAliveAsync());
    }

    [Fact]
    public void Reader_ExposesNoOrderPlacingCapability()
    {
        // AC-05: the read-only invariant is enforced at the type level. No public method may
        // reference placing/modifying/cancelling an order.
        var methods = typeof(ClientPortalBrokerReader)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Select(m => m.Name);

        string[] forbidden = ["Order", "Buy", "Sell", "Trade", "Place", "Cancel", "Modify"];
        foreach (var name in methods)
        {
            Assert.DoesNotContain(forbidden, f => name.Contains(f, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void IBrokerReader_ExposesNoOrderPlacingCapability()
    {
        var methods = typeof(IBrokerReader).GetMethods().Select(m => m.Name);
        string[] forbidden = ["Order", "Buy", "Sell", "Trade", "Place", "Cancel", "Modify"];
        foreach (var name in methods)
        {
            Assert.DoesNotContain(forbidden, f => name.Contains(f, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static HttpClient NewClient(StubHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://localhost:5000/") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder = responder;

        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return Task.FromResult(_responder(request));
        }
    }
}
