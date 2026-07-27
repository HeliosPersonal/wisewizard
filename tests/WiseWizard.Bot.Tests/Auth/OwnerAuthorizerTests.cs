using Microsoft.Extensions.Options;
using WiseWizard.Bot;
using WiseWizard.Bot.Auth;

namespace WiseWizard.Bot.Tests.Auth;

public class OwnerAuthorizerTests
{
    private static OwnerAuthorizer Create(long ownerChatId) =>
        new(Options.Create(new BotOptions { OwnerChatId = ownerChatId, BotToken = "t" }));

    [Fact]
    public void IsOwner_true_for_allowlisted_chat()
    {
        var auth = Create(42);
        Assert.True(auth.IsOwner(42));
    }

    [Fact]
    public void IsOwner_false_for_other_chat()
    {
        var auth = Create(42);
        Assert.False(auth.IsOwner(99));
    }

    [Fact]
    public void IsOwner_false_for_negative_lookalike()
    {
        var auth = Create(42);
        Assert.False(auth.IsOwner(-42));
    }

    [Fact]
    public void IsOwner_false_when_owner_unset_even_for_zero_chat()
    {
        // Defense in depth: an unset (0) owner id must never authorize chat id 0.
        var auth = Create(0);
        Assert.False(auth.IsOwner(0));
    }
}
