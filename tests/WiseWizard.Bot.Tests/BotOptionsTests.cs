using WiseWizard.Bot;

namespace WiseWizard.Bot.Tests;

public class BotOptionsTests
{
    [Theory]
    [InlineData("123:ABC", true)]
    [InlineData("  token  ", true)]
    public void HasBotToken_true_when_token_present(string token, bool expected)
    {
        var options = new BotOptions { BotToken = token };
        Assert.Equal(expected, options.HasBotToken);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void HasBotToken_false_when_token_missing_or_blank(string token)
    {
        var options = new BotOptions { BotToken = token };
        Assert.False(options.HasBotToken);
    }

    [Fact]
    public void HasBotToken_false_by_default()
    {
        Assert.False(new BotOptions().HasBotToken);
    }

    [Fact]
    public void IsValid_true_when_no_token_configured()
    {
        // A bot with no token is a valid, inactive configuration regardless of owner id.
        Assert.True(new BotOptions().IsValid());
        Assert.True(new BotOptions { OwnerChatId = 0 }.IsValid());
    }

    [Fact]
    public void IsValid_true_when_token_and_owner_set()
    {
        Assert.True(new BotOptions { BotToken = "123:ABC", OwnerChatId = 42 }.IsValid());
    }

    [Fact]
    public void IsValid_false_when_token_set_but_owner_unset()
    {
        Assert.False(new BotOptions { BotToken = "123:ABC", OwnerChatId = 0 }.IsValid());
    }
}
