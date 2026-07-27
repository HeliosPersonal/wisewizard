using WiseWizard.Bot;

namespace WiseWizard.Bot.Tests;

/// <summary>A fake <see cref="ITelegramGateway"/> that records every send and callback ack.</summary>
public sealed class RecordingGateway : ITelegramGateway
{
    public List<SentMessage> Sent { get; } = [];
    public List<string> Acked { get; } = [];

    public Task SendTextAsync(
        long chatId,
        string text,
        IReadOnlyList<(string Label, string CallbackData)>? buttons,
        CancellationToken ct = default)
    {
        Sent.Add(new SentMessage(chatId, text, buttons));
        return Task.CompletedTask;
    }

    public Task AnswerCallbackAsync(string callbackId, CancellationToken ct = default)
    {
        Acked.Add(callbackId);
        return Task.CompletedTask;
    }

    public sealed record SentMessage(
        long ChatId,
        string Text,
        IReadOnlyList<(string Label, string CallbackData)>? Buttons);
}
