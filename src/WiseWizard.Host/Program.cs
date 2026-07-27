using Hangfire;
using Hangfire.PostgreSql;
using WiseWizard.Bot;
using WiseWizard.Core;
using WiseWizard.Core.Services;
using WiseWizard.Host.Configuration;
using WiseWizard.Host.HostedServices;
using WiseWizard.Host.Jobs;
using WiseWizard.Infrastructure;
using WiseWizard.Infrastructure.Llm;
using WiseWizard.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Pull real secrets from Infisical in staging/prod; env + appsettings only in dev.
builder.AddEnvVariablesAndConfigureSecrets();

// OpenTelemetry / resilience / service discovery defaults (Aspire, ADR-0008).
builder.AddServiceDefaults();

// ── Configuration ────────────────────────────────────────────────────────────
var config = builder.Configuration;
var domainDb = config.GetConnectionString("WiseWizard")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:WiseWizard is required (PostgreSQL). Set it via Infisical (CONNECTIONSTRINGS__WISEWIZARD) "
        + "in staging/prod, or via the Aspire AppHost / user-secrets locally.");

// Anthropic settings, read once here and shared with the pipeline pricing below.
var anthropic = new AnthropicOptions
{
    ApiKey = config["Anthropic:ApiKey"] ?? string.Empty,
    CheapModel = config["Anthropic:CheapModel"] ?? "claude-haiku-4-5-20251001",
    SynthesisModel = config["Anthropic:SynthesisModel"] ?? "claude-opus-4-8",
    CheapInputPerMillionUsd = config.GetValue("Anthropic:CheapInputPerMillionUsd", 1.00m),
    CheapOutputPerMillionUsd = config.GetValue("Anthropic:CheapOutputPerMillionUsd", 5.00m),
    SynthesisInputPerMillionUsd = config.GetValue("Anthropic:SynthesisInputPerMillionUsd", 15.00m),
    SynthesisOutputPerMillionUsd = config.GetValue("Anthropic:SynthesisOutputPerMillionUsd", 75.00m),
};

// ── Dependency injection ─────────────────────────────────────────────────────
// Options are bound + validated-on-start inside each layer's registration (fail fast at startup).
builder.Services.AddWiseWizardCore(o =>
{
    o.CostCeilingUsd = config.GetValue("Pipeline:CostCeilingUsd", 2.00m);
    o.MaxWallClock = TimeSpan.FromHours(config.GetValue("Pipeline:MaxWallClockHours", 20));
    o.CheapPricing = new TierPricing
    {
        InputPerMillionUsd = anthropic.CheapInputPerMillionUsd,
        OutputPerMillionUsd = anthropic.CheapOutputPerMillionUsd,
    };
    o.SynthesisPricing = new TierPricing
    {
        InputPerMillionUsd = anthropic.SynthesisInputPerMillionUsd,
        OutputPerMillionUsd = anthropic.SynthesisOutputPerMillionUsd,
    };
});
builder.Services.AddWiseWizardInfrastructure(new InfrastructureOptions
{
    DomainConnectionString = domainDb,
    IbkrGatewayBaseUrl = config["Ibkr:GatewayBaseUrl"] ?? "https://localhost:5000/",
    IbkrAccountId = config["Ibkr:AccountId"] ?? string.Empty,
    SecUserAgent = config["Ingestion:SecUserAgent"] ?? "WiseWizard/1.0 (personal use)",
    Anthropic = anthropic,
});
builder.Services.AddWiseWizardBot(o =>
{
    o.OwnerChatId = config.GetValue<long>("Telegram:OwnerChatId");
    o.BotToken = config["Telegram:BotToken"] ?? string.Empty;
});

// ── Hangfire (nightly cascade scheduling, persistence, retries) ──────────────
// Shares the domain PostgreSQL database under a dedicated `hangfire` schema (ADR-0007).
builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(domainDb),
        new PostgreSqlStorageOptions { SchemaName = "hangfire", PrepareSchemaIfNecessary = true }));
builder.Services.AddHangfireServer();
builder.Services.AddScoped<NightlyPipelineJob>();

// ── Health checks (k8s liveness/readiness probes) ────────────────────────────
builder.Services.AddHealthChecks();

// ── Hosted services ──────────────────────────────────────────────────────────
builder.Services.AddHostedService<BrokerKeepAliveService>();
builder.Services.AddHostedService<TelegramPollingService>();
builder.Services.AddHostedService<StartupInitializer>();

var app = builder.Build();

// Liveness/readiness endpoint for Kubernetes probes.
app.MapHealthChecks("/health");

// Hangfire dashboard — bound to the pod only; expose via port-forward, never Ingress.
app.MapHangfireDashboard("/hangfire");

app.Run();
