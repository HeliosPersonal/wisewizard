namespace WiseWizard.Bot;

/// <summary>
/// A no-op <see cref="ITelegramGateway"/> used when no bot token is configured, so the Host and the
/// pipeline/broker services still start and run. Outbound messages are silently dropped.
/// </summary>
public sealed class NullTelegramGateway : ITelegramGateway
{
    public Task SendTextAsync(
        long chatId,
        string text,
        IReadOnlyList<(string Label, string CallbackData)>? buttons,
        CancellationToken ct = default) => Task.CompletedTask;

    public Task AnswerCallbackAsync(string callbackId, CancellationToken ct = default) => Task.CompletedTask;
}
