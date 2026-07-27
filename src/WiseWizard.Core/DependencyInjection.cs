using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WiseWizard.Core.Abstractions;
using WiseWizard.Core.Services;

namespace WiseWizard.Core;

/// <summary>Registers Core domain services and the default clock.</summary>
[ExcludeFromCodeCoverage(Justification = "Composition-root wiring; exercised end-to-end via the Host, not unit-tested.")]
public static class DependencyInjection
{
    public static IServiceCollection AddWiseWizardCore(
        this IServiceCollection services, Action<PipelineOptions> configure)
    {
        // Bind + validate-on-start: a bad limit (non-positive ceiling/timeout, negative pricing)
        // fails fast during host startup via the options IStartupValidator, not at run time.
        services.AddOptions<PipelineOptions>()
            .Configure(configure)
            .Validate(o => o.IsValid(),
                "PipelineOptions is invalid: CostCeilingUsd and MaxWallClock must be positive and pricing non-negative.")
            .ValidateOnStart();

        // Domain services consume the plain record; resolve it from the validated options.
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<PipelineOptions>>().Value);

        services.AddSingleton<IClock, SystemClock>();

        // Domain services — scoped so they share the request/job's repository scope.
        services.AddScoped<WatchlistService>();
        services.AddScoped<UniverseProvider>();
        services.AddScoped<PortfolioRefreshService>();
        services.AddScoped<KeepAliveService>();
        services.AddScoped<IngestionService>();
        services.AddScoped<RetentionService>();
        services.AddScoped<CheapTierExtractionStep>();
        services.AddScoped<SynthesisStep>();
        services.AddScoped<NightlyRunOrchestrator>();

        return services;
    }
}
