using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WiseWizard.Core.Abstractions;
using WiseWizard.Infrastructure.Ibkr;
using WiseWizard.Infrastructure.Ingestion;
using WiseWizard.Infrastructure.Llm;
using WiseWizard.Infrastructure.Market;
using WiseWizard.Infrastructure.News;
using WiseWizard.Infrastructure.Persistence;
using WiseWizard.Infrastructure.Sec;

namespace WiseWizard.Infrastructure;

/// <summary>Configuration for the Infrastructure layer, bound from the Host configuration.</summary>
[ExcludeFromCodeCoverage(Justification = "Composition-root configuration record populated by the Host.")]
public sealed record InfrastructureOptions
{
    /// <summary>Domain PostgreSQL connection string (e.g. <c>Host=...;Database=...;Username=...;Password=...</c>).</summary>
    public required string DomainConnectionString { get; init; }

    /// <summary>Base address of the local IBKR Client Portal gateway.</summary>
    public string IbkrGatewayBaseUrl { get; init; } = "https://localhost:5000/";

    /// <summary>The IBKR account id whose Positions are read.</summary>
    public string IbkrAccountId { get; init; } = string.Empty;

    /// <summary>Contact string used in the SEC EDGAR User-Agent (fair-access requirement).</summary>
    public string SecUserAgent { get; init; } = "WiseWizard/1.0 (personal use)";

    /// <summary>Anthropic settings (API key, model ids, pricing).</summary>
    public required AnthropicOptions Anthropic { get; init; }
}

/// <summary>Registers Infrastructure implementations of the Core abstractions.</summary>
[ExcludeFromCodeCoverage(Justification = "Composition-root wiring; exercised end-to-end via the Host, not unit-tested.")]
public static class DependencyInjection
{
    public static IServiceCollection AddWiseWizardInfrastructure(
        this IServiceCollection services, InfrastructureOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DomainConnectionString);

        // Anthropic settings flow through the options pipeline so their shape is validated at
        // startup (ValidateOnStart); the LLM client consumes the plain record resolved from it.
        var anthropic = options.Anthropic;
        services.AddOptions<AnthropicOptions>()
            .Configure(o =>
            {
                o.ApiKey = anthropic.ApiKey;
                o.CheapModel = anthropic.CheapModel;
                o.SynthesisModel = anthropic.SynthesisModel;
                o.CheapInputPerMillionUsd = anthropic.CheapInputPerMillionUsd;
                o.CheapOutputPerMillionUsd = anthropic.CheapOutputPerMillionUsd;
                o.SynthesisInputPerMillionUsd = anthropic.SynthesisInputPerMillionUsd;
                o.SynthesisOutputPerMillionUsd = anthropic.SynthesisOutputPerMillionUsd;
                o.MaxTokens = anthropic.MaxTokens;
            })
            .Validate(o => o.IsValid(),
                "AnthropicOptions is invalid: model ids must be set, pricing non-negative, MaxTokens positive.")
            .ValidateOnStart();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<AnthropicOptions>>().Value);

        // Persistence — one shared connection factory over the domain PostgreSQL database.
        services.AddSingleton<IDbConnectionFactory>(
            new NpgsqlConnectionFactory(options.DomainConnectionString));

        services.AddScoped<IPositionsRepository, PositionsRepository>();
        services.AddScoped<IBrokerSessionRepository, BrokerSessionRepository>();
        services.AddScoped<IWatchlistRepository, WatchlistRepository>();
        services.AddScoped<IRawDocumentRepository, RawDocumentRepository>();
        services.AddScoped<IRunRepository, RunRepository>();
        services.AddScoped<IExtractedFactRepository, ExtractedFactRepository>();
        services.AddScoped<IVerdictRepository, VerdictRepository>();
        services.AddScoped<IBotDeliveryLog, BotDeliveryLogRepository>();

        // Rate limiting — polite pacing per host; ~10 req/s ceiling shared by Sources.
        services.AddSingleton<IRateLimiter>(sp =>
            new TokenBucketRateLimiter(
                sp.GetRequiredService<IClock>(),
                TokenBucketRateLimiter.IntervalForRatePerSecond(10)));

        // Data Sources — each over its own named HttpClient.
        services.AddHttpClient<ISecFilingsSource, EdgarFilingsSource>(c =>
        {
            c.BaseAddress = new Uri("https://data.sec.gov/");
            c.DefaultRequestHeaders.UserAgent.ParseAdd(options.SecUserAgent);
        });
        services.AddHttpClient<INewsSource, RssNewsSource>();
        services.AddHttpClient<IMarketDataSource, MarketDataSource>();

        // IDataSource fan-out: the ingestion service consumes IEnumerable<IDataSource>.
        services.AddScoped<IDataSource>(sp => sp.GetRequiredService<ISecFilingsSource>());
        services.AddScoped<IDataSource>(sp => sp.GetRequiredService<INewsSource>());
        services.AddScoped<IDataSource>(sp => sp.GetRequiredService<IMarketDataSource>());

        // LLM client over the Anthropic Batch API (options registered + validated above).
        services.AddHttpClient<ILlmClient, AnthropicLlmClient>(c =>
        {
            c.BaseAddress = new Uri("https://api.anthropic.com/");
            c.DefaultRequestHeaders.Add("x-api-key", anthropic.ApiKey);
            c.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        // Broker reader over the local Client Portal gateway.
        services.AddHttpClient<IBrokerReader>(c => c.BaseAddress = new Uri(options.IbkrGatewayBaseUrl))
            .AddTypedClient<IBrokerReader>((http, _) =>
                new ClientPortalBrokerReader(http, options.IbkrAccountId));

        return services;
    }
}
