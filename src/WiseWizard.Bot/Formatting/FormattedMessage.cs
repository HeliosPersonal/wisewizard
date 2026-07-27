namespace WiseWizard.Bot.Formatting;

/// <summary>
/// One ready-to-send message: pre-escaped/pre-formatted text plus its inline buttons. The digest
/// may span several of these in order (PRD AC-09 chunking); other formatters produce a single one.
/// </summary>
/// <param name="Text">The MarkdownV2 message body, already within Telegram's size ceiling.</param>
/// <param name="Buttons">The inline buttons for this message, as (label, callbackData) pairs.</param>
public sealed record FormattedMessage(
    string Text,
    IReadOnlyList<(string Label, string CallbackData)> Buttons);
