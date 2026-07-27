using WiseWizard.Bot.Formatting;
using WiseWizard.Core.Models;

namespace WiseWizard.Bot.Tests.Formatting;

public class PortfolioFormatterTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 7, 26, 6, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    private static Position P(string ticker, decimal qty, decimal mv, decimal pnl) => new()
    {
        Ticker = Ticker.Create(ticker),
        Quantity = qty,
        AvgCost = 10m,
        MarketValue = mv,
        UnrealizedPnl = pnl,
        AsOf = AsOf,
    };

    [Fact]
    public void Empty_portfolio_returns_empty_state_message()
    {
        var msg = PortfolioFormatter.Format([], null, Now);

        Assert.Contains("no open positions", msg.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(msg.Buttons);
    }

    [Fact]
    public void Renders_each_position_with_quantity_market_value_and_pnl()
    {
        var msg = PortfolioFormatter.Format([P("AAPL", 10m, 2000m, 150.25m)], null, Now);

        Assert.Contains("AAPL", msg.Text);
        Assert.Contains("10", msg.Text);
        Assert.Contains("2000", msg.Text);
        Assert.Contains("150", msg.Text);
    }

    [Fact]
    public void Includes_total_pnl_across_positions()
    {
        var msg = PortfolioFormatter.Format(
            [P("AAPL", 10m, 2000m, 100m), P("MSFT", 5m, 1500m, -40m)], null, Now);

        // Net total 60 should appear in a totals line.
        Assert.Contains("60", msg.Text);
    }

    [Fact]
    public void Notes_snapshot_age_from_as_of_when_session_state_absent()
    {
        var msg = PortfolioFormatter.Format([P("AAPL", 10m, 2000m, 100m)], null, Now);

        // 6 hours between AsOf and Now.
        Assert.Contains("as of", msg.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Adds_stale_note_when_session_is_lapsed()
    {
        var session = new BrokerSessionState { Status = SessionStatus.Lapsed, LastSnapshotAt = AsOf };

        var msg = PortfolioFormatter.Format([P("AAPL", 10m, 2000m, 100m)], session, Now);

        Assert.Contains("re\\-authentication", msg.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Live_session_does_not_add_stale_note()
    {
        var session = new BrokerSessionState { Status = SessionStatus.Live, LastSnapshotAt = AsOf };

        var msg = PortfolioFormatter.Format([P("AAPL", 10m, 2000m, 100m)], session, Now);

        Assert.DoesNotContain("re-authentication", msg.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Special_characters_in_ticker_are_escaped()
    {
        var msg = PortfolioFormatter.Format([P("BRK.B", 1m, 500m, 10m)], null, Now);

        Assert.Contains("BRK\\.B", msg.Text);
    }

    [Fact]
    public void Age_just_now_when_snapshot_is_current()
    {
        var pos = P("AAPL", 1m, 100m, 0m) with { AsOf = Now };
        var msg = PortfolioFormatter.Format([pos], null, Now);
        Assert.Contains("just now", msg.Text);
    }

    [Fact]
    public void Age_minutes_ago()
    {
        var pos = P("AAPL", 1m, 100m, 0m) with { AsOf = Now - TimeSpan.FromMinutes(30) };
        var msg = PortfolioFormatter.Format([pos], null, Now);
        Assert.Contains("30m ago", msg.Text);
    }

    [Fact]
    public void Age_hours_ago()
    {
        var pos = P("AAPL", 1m, 100m, 0m) with { AsOf = Now - TimeSpan.FromHours(5) };
        var msg = PortfolioFormatter.Format([pos], null, Now);
        Assert.Contains("5h ago", msg.Text);
    }

    [Fact]
    public void Age_days_ago()
    {
        var pos = P("AAPL", 1m, 100m, 0m) with { AsOf = Now - TimeSpan.FromDays(3) };
        var msg = PortfolioFormatter.Format([pos], null, Now);
        Assert.Contains("3d ago", msg.Text);
    }

    [Fact]
    public void Age_future_snapshot_is_clamped_to_just_now()
    {
        var pos = P("AAPL", 1m, 100m, 0m) with { AsOf = Now + TimeSpan.FromHours(2) };
        var msg = PortfolioFormatter.Format([pos], null, Now);
        Assert.Contains("just now", msg.Text);
    }
}
