using System.Diagnostics.CodeAnalysis;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace WiseWizard.Bot;

/// <summary>
/// Thin adapter mapping <see cref="ITelegramGateway"/> onto Telegram.Bot's
/// <see cref="ITelegramBotClient"/>. Carries no business logic — it only translates our
/// (label, callbackData) button pairs into a single-column inline keyboard and forwards the send.
/// Excluded from coverage as an untestable I/O boundary (kept logic-free per the &gt;95% gate).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class TelegramBotGateway(ITelegramBotClient client) : ITelegramGateway
{
    private readonly ITelegramBotClient _client = client;

    public async Task SendTextAsync(
        long chatId,
        string text,
        IReadOnlyList<(string Label, string CallbackData)>? buttons,
        CancellationToken ct = default)
    {
        InlineKeyboardMarkup? markup = null;
        if (buttons is { Count: > 0 })
        {
            var rows = buttons.Select(b => new[] { InlineKeyboardButton.WithCallbackData(b.Label, b.CallbackData) });
            markup = new InlineKeyboardMarkup(rows);
        }

        await _client.SendMessage(
            chatId,
            text,
            parseMode: ParseMode.MarkdownV2,
            replyMarkup: markup,
            cancellationToken: ct);
    }

    public async Task AnswerCallbackAsync(string callbackId, CancellationToken ct = default)
    {
        await _client.AnswerCallbackQuery(callbackId, cancellationToken: ct);
    }
}
