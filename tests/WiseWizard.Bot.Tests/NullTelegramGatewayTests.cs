using WiseWizard.Bot;

namespace WiseWizard.Bot.Tests;

public class NullTelegramGatewayTests
{
    [Fact]
    public async Task SendTextAsync_completes_without_error()
    {
        var gateway = new NullTelegramGateway();
        await gateway.SendTextAsync(1, "hello", null);
        await gateway.SendTextAsync(1, "hi", [("Label", "cb")]);
    }

    [Fact]
    public async Task AnswerCallbackAsync_completes_without_error()
    {
        var gateway = new NullTelegramGateway();
        await gateway.AnswerCallbackAsync("cb-1");
    }
}
