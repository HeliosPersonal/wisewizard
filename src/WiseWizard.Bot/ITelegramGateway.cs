namespace WiseWizard.Bot;

/// <summary>
/// The only surface that touches Telegram. Handlers depend on this abstraction so they are unit
/// testable without a live bot. Buttons are given as (label, callbackData) pairs; the
/// implementation renders them as a single-column inline keyboard.
/// </summary>
public interface ITelegramGateway
{
    /// <summary>
    /// Sends a text message to a chat, optionally with an inline keyboard. Text is expected to be
    /// pre-escaped/pre-formatted by the formatters.
    /// </summary>
    Task SendTextAsync(
        long chatId,
        string text,
        IReadOnlyList<(string Label, string CallbackData)>? buttons,
        CancellationToken ct = default);

    /// <summary>Acknowledges an inline-button tap so the client stops its loading spinner.</summary>
    Task AnswerCallbackAsync(string callbackId, CancellationToken ct = default);
}
