using System.Text;
using WiseWizard.Core.Models;

namespace WiseWizard.Bot.Formatting;

/// <summary>
/// Pure formatter for a Ticker drill-down (AC-02): the Signal, full reasoning, "what changed",
/// and the cited Sources so the Signal can be audited against its evidence. Also renders the
/// "no Verdict for this Ticker in the latest report" case (AC-02b). All dynamic text is escaped.
/// </summary>
public static class DetailFormatter
{
    /// <summary>Renders the full drill-down for a Ticker that has a Verdict in the latest Run.</summary>
    public static FormattedMessage Format(Verdict verdict)
    {
        ArgumentNullException.ThrowIfNull(verdict);

        var sb = new StringBuilder();
        sb.Append(verdict.Signal.ToEmoji()).Append(' ')
          .Append(TelegramText.Bold(verdict.Ticker.Value)).Append('\n');

        sb.Append('\n').Append(TelegramText.Escape(verdict.FullReasoning));

        sb.Append('\n').Append('\n')
          .Append(TelegramText.Bold("What changed")).Append('\n')
          .Append(TelegramText.Escape(verdict.ChangeFromYesterday));

        sb.Append('\n').Append('\n').Append(TelegramText.Bold("Sources"));
        if (verdict.Sources.Count == 0)
        {
            sb.Append('\n').Append(TelegramText.Escape("(none cited)"));
        }
        else
        {
            foreach (var source in verdict.Sources)
            {
                sb.Append('\n').Append(TelegramText.Escape($"• {source}"));
            }
        }

        return new FormattedMessage(sb.ToString(), []);
    }

    /// <summary>Renders the AC-02b message when the Ticker has no Verdict in the latest Run.</summary>
    public static FormattedMessage FormatAbsent(Ticker ticker)
    {
        var text = TelegramText.Escape(
            $"No verdict for {ticker.Value} in the latest report.");
        return new FormattedMessage(text, []);
    }
}
