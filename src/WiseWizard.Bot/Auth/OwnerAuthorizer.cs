using Microsoft.Extensions.Options;

namespace WiseWizard.Bot.Auth;

/// <summary>
/// Single-Owner chat-id allowlist. Only the configured Owner chat may interact with the bot;
/// every other chat is ignored, revealing nothing (PRD AC-05).
/// </summary>
public sealed class OwnerAuthorizer(IOptions<BotOptions> options)
{
    private readonly long _ownerChatId = options.Value.OwnerChatId;

    /// <summary>
    /// True only when <paramref name="chatId"/> is the allowlisted Owner chat. An unset Owner id (0)
    /// is never an owner, so a misconfigured allowlist cannot authorize chat id 0 (defense in depth
    /// alongside <see cref="BotOptions.Validate"/>).
    /// </summary>
    public bool IsOwner(long chatId) => _ownerChatId != 0 && chatId == _ownerChatId;
}
