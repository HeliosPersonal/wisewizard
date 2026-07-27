using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using WiseWizard.Bot.Auth;
using WiseWizard.Bot.Handlers;
using WiseWizard.Core.Abstractions;

namespace WiseWizard.Bot;

/// <summary>Registers the Telegram bot gateway, handlers, router, and the Owner notifier.</summary>
[ExcludeFromCodeCoverage(Justification = "Composition-root wiring; exercised end-to-end via the Host, not unit-tested.")]
public static class DependencyInjection
{
    public static IServiceCollection AddWiseWizardBot(
        this IServiceCollection services, Action<BotOptions> configure)
    {
        // Bind + validate-on-start: a configured token with an unset Owner chat id (which would
        // authorize chat id 0) fails fast at startup via the options IStartupValidator.
        services.AddOptions<BotOptions>()
            .Configure(configure)
            .Validate(o => o.IsValid(),
                "BotOptions.OwnerChatId must be a non-zero Telegram chat id when a BotToken is configured.")
            .ValidateOnStart();

        // The concrete gateway choice depends on whether a token is configured. Resolve the bound
        // options once here to branch the registration; validation still runs at ValidateOnStart.
        var options = new BotOptions();
        configure(options);

        if (options.HasBotToken)
        {
            services.AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient(options.BotToken));
            services.AddSingleton<ITelegramGateway, TelegramBotGateway>();
        }
        else
        {
            // No token configured: use a no-op gateway so the Host starts and the pipeline/broker
            // services still run — alerts and replies are silently dropped until a token is set.
            services.AddSingleton<ITelegramGateway, NullTelegramGateway>();
        }

        services.AddSingleton<OwnerAuthorizer>();

        // Handlers + router are scoped so they resolve scoped repositories per update/job.
        services.AddScoped<PortfolioHandler>();
        services.AddScoped<ReportHandler>();
        services.AddScoped<DrillDownHandler>();
        services.AddScoped<WatchlistCommandHandler>();
        services.AddScoped<CommandRouter>();

        // The bot delivers Owner alerts for the pipeline and broker session keeper.
        services.AddScoped<IOwnerNotifier, TelegramOwnerNotifier>();

        return services;
    }
}
