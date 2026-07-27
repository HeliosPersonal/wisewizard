using System.Globalization;
using System.Text;
using WiseWizard.Core.Models;

namespace WiseWizard.Bot.Formatting;

/// <summary>
/// Pure formatter for the Portfolio summary (AC-03): each Position with its holding and P&amp;L,
/// a net total, and how current the snapshot is (its age, plus a re-auth note when the session
/// has lapsed). Handles the empty-portfolio state. All dynamic text is MarkdownV2-escaped.
/// </summary>
public static class PortfolioFormatter
{
    /// <summary>Shown when the current snapshot holds no Positions.</summary>
    public const string EmptyMessage = "Your portfolio has no open positions.";

    /// <summary>Renders the Portfolio summary into a single message (no buttons).</summary>
    public static FormattedMessage Format(
        IReadOnlyList<Position> positions,
        BrokerSessionState? session,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(positions);

        if (positions.Count == 0)
        {
            return new FormattedMessage(TelegramText.Escape(EmptyMessage), []);
        }

        var sb = new StringBuilder();
        sb.Append(TelegramText.Bold("Portfolio")).Append('\n');

        decimal totalPnl = 0m;
        var asOf = positions[0].AsOf;
        foreach (var p in positions)
        {
            totalPnl += p.UnrealizedPnl;
            sb.Append('\n').Append(RenderPosition(p));
        }

        sb.Append('\n').Append('\n')
          .Append(TelegramText.Escape($"Net unrealized P&L: {Money(totalPnl)}"));

        sb.Append('\n').Append(TelegramText.Escape($"Snapshot as of {asOf.UtcDateTime:yyyy-MM-dd HH:mm} UTC ({Age(asOf, now)})"));

        if (session?.Status == SessionStatus.Lapsed)
        {
            sb.Append('\n').Append(TelegramText.Escape(
                "Note: brokerage session lapsed — a re-authentication tap is needed for a fresh snapshot."));
        }

        return new FormattedMessage(sb.ToString(), []);
    }

    private static string RenderPosition(Position p)
    {
        var line = $"{p.Ticker.Value}: {Qty(p.Quantity)} @ mkt {Money(p.MarketValue)} ({Sign(p.UnrealizedPnl)}{Money(p.UnrealizedPnl)})";
        return TelegramText.Escape(line);
    }

    private static string Money(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Qty(decimal value) =>
        value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string Sign(decimal value) => value >= 0 ? "+" : string.Empty;

    private static string Age(DateTimeOffset asOf, DateTimeOffset now)
    {
        var span = now - asOf;
        if (span < TimeSpan.Zero)
        {
            span = TimeSpan.Zero;
        }

        if (span.TotalMinutes < 1)
        {
            return "just now";
        }

        if (span.TotalHours < 1)
        {
            return $"{(int)span.TotalMinutes}m ago";
        }

        if (span.TotalDays < 1)
        {
            return $"{(int)span.TotalHours}h ago";
        }

        return $"{(int)span.TotalDays}d ago";
    }
}
