using System.Text;
using WiseWizard.Core.Models;

namespace WiseWizard.Bot.Formatting;

/// <summary>
/// Pure formatter for the Daily digest: one line per Ticker (emoji + Ticker + one-phrase reason)
/// with a per-Ticker "details" button. Enforces the PRD NFR ceilings by chunking at Ticker
/// boundaries — ≤ 20 Ticker lines and ≤ 4000 characters per message (AC-09) — and renders the
/// empty state when no completed Run exists (AC-04). All dynamic text is MarkdownV2-escaped.
/// </summary>
public static class DigestFormatter
{
    /// <summary>Maximum Ticker lines per message (PRD §6).</summary>
    public const int MaxLinesPerMessage = 20;

    /// <summary>Maximum characters per message (PRD §6).</summary>
    public const int MaxCharsPerMessage = 4000;

    /// <summary>Shown when no completed Run exists yet (AC-04).</summary>
    public const string NoDigestMessage = "No digest available yet — the first nightly Run has not completed.";

    /// <summary>Renders the digest into one or more ordered messages.</summary>
    public static IReadOnlyList<FormattedMessage> Format(IReadOnlyList<Verdict> verdicts)
    {
        ArgumentNullException.ThrowIfNull(verdicts);

        if (verdicts.Count == 0)
        {
            return [new FormattedMessage(TelegramText.Escape(NoDigestMessage), [])];
        }

        var ordered = verdicts
            .OrderBy(v => SignalRank(v.Signal))
            .ThenBy(v => v.Ticker.Value, StringComparer.Ordinal)
            .ToList();

        var messages = new List<FormattedMessage>();
        var sb = new StringBuilder();
        var buttons = new List<(string Label, string CallbackData)>();

        foreach (var v in ordered)
        {
            var line = RenderLine(v);

            var wouldOverflowLines = buttons.Count >= MaxLinesPerMessage;
            var wouldOverflowChars = sb.Length > 0 && sb.Length + 1 + line.Length > MaxCharsPerMessage;

            if (wouldOverflowLines || wouldOverflowChars)
            {
                messages.Add(new FormattedMessage(sb.ToString(), buttons));
                sb = new StringBuilder();
                buttons = [];
            }

            if (sb.Length > 0)
            {
                sb.Append('\n');
            }

            sb.Append(line);
            buttons.Add(($"{v.Signal.ToEmoji()} {v.Ticker.Value}", $"detail:{v.Ticker.Value}"));
        }

        messages.Add(new FormattedMessage(sb.ToString(), buttons));
        return messages;
    }

    private static string RenderLine(Verdict v) =>
        $"{v.Signal.ToEmoji()} {TelegramText.Bold(v.Ticker.Value)} — {TelegramText.Escape(v.SummaryLine)}";

    // Review (🔴) is most urgent, then Attention (🟡), then Hold (🟢).
    private static int SignalRank(Signal signal) => signal switch
    {
        Signal.Review => 0,
        Signal.Attention => 1,
        Signal.Hold => 2,
        _ => 3,
    };
}
