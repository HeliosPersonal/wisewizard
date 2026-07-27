namespace WiseWizard.Bot;

/// <summary>
/// A transport-neutral inbound text command from a chat. The gateway/hosted service maps a
/// Telegram <c>Update</c> into this record so no Telegram.Bot type leaks into routing/handler logic.
/// </summary>
/// <param name="ChatId">The originating chat id, authorized against the Owner allowlist.</param>
/// <param name="Text">The raw message text (e.g. <c>/report</c>, <c>/watch AAPL note</c>).</param>
public sealed record IncomingMessage(long ChatId, string Text);

/// <summary>
/// A transport-neutral inbound callback (inline-button tap). Carries the callback query id so
/// the tap can be acknowledged, and the callback data (e.g. <c>detail:AAPL</c>).
/// </summary>
/// <param name="ChatId">The originating chat id, authorized against the Owner allowlist.</param>
/// <param name="CallbackId">The Telegram callback query id, used to acknowledge the tap.</param>
/// <param name="Data">The callback data payload set on the inline button.</param>
public sealed record IncomingCallback(long ChatId, string CallbackId, string Data);
