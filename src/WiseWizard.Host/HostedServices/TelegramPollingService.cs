using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WiseWizard.Bot;
using WiseWizard.Bot.Handlers;

namespace WiseWizard.Host.HostedServices;

/// <summary>
/// Long-polls Telegram for updates and dispatches each to the <see cref="CommandRouter"/> within a
/// DI scope. The transport/mapping is thin; all routing and authorization logic lives in the router.
/// When no bot token is configured the service stays idle so the rest of the Host still runs.
/// </summary>
public sealed class TelegramPollingService(
    IServiceProvider services,
    IOptions<BotOptions> botOptions,
    IServiceScopeFactory scopeFactory,
    ILogger<TelegramPollingService> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!botOptions.Value.HasBotToken)
        {
            logger.LogWarning("Telegram polling disabled: no bot token configured");
            return Task.CompletedTask;
        }

        var client = services.GetRequiredService<ITelegramBotClient>();
        var options = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery],
        };

        client.StartReceiving(HandleUpdateAsync, HandleErrorAsync, options, stoppingToken);
        logger.LogInformation("Telegram polling started");
        return Task.CompletedTask;
    }

    private async Task HandleUpdateAsync(ITelegramBotClient _, Update update, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var router = scope.ServiceProvider.GetRequiredService<CommandRouter>();

        var message = TelegramUpdateMapper.ToMessage(update);
        if (message is not null)
        {
            await router.RouteMessageAsync(message, ct);
            return;
        }

        var callback = TelegramUpdateMapper.ToCallback(update);
        if (callback is not null)
        {
            await router.RouteCallbackAsync(callback, ct);
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient _, Exception exception, CancellationToken ct)
    {
        logger.LogError(exception, "Telegram polling error");
        return Task.CompletedTask;
    }
}
