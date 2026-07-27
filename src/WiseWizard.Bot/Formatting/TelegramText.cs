using System.Buffers;
using System.Text;

namespace WiseWizard.Bot.Formatting;

/// <summary>
/// Pure MarkdownV2 escaping/formatting helpers. All dynamic values (Ticker symbols, notes,
/// reasoning, Source titles) must be escaped before rendering so no user- or Source-supplied
/// text can alter message formatting or embed active content (PRD §6.1 abuse case).
/// </summary>
public static class TelegramText
{
    // The full set of characters Telegram MarkdownV2 requires to be backslash-escaped.
    private static readonly SearchValues<char> Special =
        SearchValues.Create("_*[]()~`>#+-=|{}.!\\");

    /// <summary>Escapes every MarkdownV2 special character in <paramref name="text"/>.</summary>
    public static string Escape(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(text.Length + 8);
        foreach (var c in text)
        {
            if (Special.Contains(c))
            {
                sb.Append('\\');
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    /// <summary>Escapes <paramref name="text"/> then wraps it in MarkdownV2 bold markers.</summary>
    public static string Bold(string? text) => $"*{Escape(text)}*";
}
