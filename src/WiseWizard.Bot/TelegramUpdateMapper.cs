using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types;

namespace WiseWizard.Bot;

/// <summary>
/// Maps a Telegram.Bot <see cref="Update"/> into our transport-neutral records so no Telegram type
/// leaks into routing/handler logic. Kept trivial and excluded from coverage: constructing the
/// framework's <see cref="Update"/> graph in a unit test is awkward, and all real routing logic
/// lives in the (fully tested) <c>CommandRouter</c> and handlers.
/// </summary>
[ExcludeFromCodeCoverage]
public static class TelegramUpdateMapper
{
    /// <summary>Extracts a text message, or null when the update carries none.</summary>
    public static IncomingMessage? ToMessage(Update update)
    {
        var message = update.Message;
        if (message?.Text is { } text && message.Chat is { } chat)
        {
            return new IncomingMessage(chat.Id, text);
        }

        return null;
    }

    /// <summary>Extracts a callback (inline-button tap), or null when the update carries none.</summary>
    public static IncomingCallback? ToCallback(Update update)
    {
        var callback = update.CallbackQuery;
        if (callback?.Data is { } data && callback.Message?.Chat is { } chat)
        {
            return new IncomingCallback(chat.Id, callback.Id, data);
        }

        return null;
    }
}
