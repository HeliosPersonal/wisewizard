namespace WiseWizard.Bot;

/// <summary>
/// Bot configuration: the single allowlisted Owner chat id and the Telegram bot token.
/// Bound from configuration in the Host composition root.
/// </summary>
public sealed class BotOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "Telegram";

    /// <summary>The single Owner's Telegram chat id; the only chat the bot answers (AC-05).</summary>
    public long OwnerChatId { get; set; }

    /// <summary>The Telegram bot API token used to construct the bot client.</summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>
    /// True when a bot token is configured. When false the Host still starts, but the Telegram
    /// polling service does not begin receiving — the bot is simply inactive.
    /// </summary>
    public bool HasBotToken => !string.IsNullOrWhiteSpace(BotToken);

    /// <summary>
    /// The single-Owner invariant (AC-05): a live bot (token configured) must have a non-zero
    /// <see cref="OwnerChatId"/>, otherwise it would authorize chat id 0. A bot with no token is a
    /// valid, inactive configuration. Wired via <c>Validate(...).ValidateOnStart()</c> so the bad
    /// combination fails fast at startup.
    /// </summary>
    public bool IsValid() => !HasBotToken || OwnerChatId != 0;
}
