// WiseWizard local-dev orchestration (.NET Aspire, ADR-0008). One command brings up everything
// the app needs for local development:
//   dotnet run --project src/Aspire/WiseWizard.AppHost
// It provisions a PostgreSQL container (persistent volume + pgweb), injects its connection string
// into the Host as ConnectionStrings:WiseWizard, and passes the app secrets (Anthropic / Telegram /
// IBKR) from `dotnet user-secrets` so nothing sensitive is committed. Infisical stays the source
// of secrets for staging/prod only.
var builder = DistributedApplication.CreateBuilder(args);

// A STABLE dev password (not Aspire's per-run random one) is required alongside a persistent
// volume: Postgres only honours the password when it first initialises the data dir, so a fresh
// random password on the next run would fail auth against the already-initialised volume. Sourced
// from user-secrets ("Parameters:postgres-password"); a default in appsettings.Development.json
// keeps a clean checkout working.
var postgresPassword = builder.AddParameter("postgres-password", secret: true);

var postgres = builder.AddPostgres("postgres", password: postgresPassword)
    .WithDataVolume("wisewizard-pgdata")
    .WithPgWeb();

// The domain database. Named "wisewizard" so the injected connection string resolves to
// ConnectionStrings:WiseWizard in the Host (config keys are case-insensitive). Hangfire shares
// this same database under a dedicated `hangfire` schema (ADR-0007).
var domainDb = postgres.AddDatabase("wisewizard");

// App secrets from user-secrets (gitignored). Optional in dev — the app degrades gracefully when
// a key is absent (no Telegram token → bot disabled; no Anthropic key → pipeline no-ops). Set with:
//   dotnet user-secrets set "Parameters:anthropic-api-key" "sk-ant-..."
//   dotnet user-secrets set "Parameters:telegram-bot-token" "123:ABC"
//   dotnet user-secrets set "Parameters:telegram-owner-chat-id" "123456789"
//   dotnet user-secrets set "Parameters:ibkr-account-id" "U1234567"
var anthropicApiKey = builder.AddParameter("anthropic-api-key", secret: true);
var telegramBotToken = builder.AddParameter("telegram-bot-token", secret: true);
var telegramOwnerChatId = builder.AddParameter("telegram-owner-chat-id");
var ibkrAccountId = builder.AddParameter("ibkr-account-id");
var infisicalClientId = builder.AddParameter("infisical-client-id", secret: true);
var infisicalClientSecret = builder.AddParameter("infisical-client-secret", secret: true);
var infisicalProjectId = builder.AddParameter("infisical-project-id", secret: true);

builder.AddProject<Projects.WiseWizard_Host>("wisewizard")
    .WithReference(domainDb)
    .WaitFor(domainDb)
    // App config, mapped to the Host's configuration keys (double-underscore = section separator).
    .WithEnvironment("Anthropic__ApiKey", anthropicApiKey)
    .WithEnvironment("Telegram__BotToken", telegramBotToken)
    .WithEnvironment("Telegram__OwnerChatId", telegramOwnerChatId)
    .WithEnvironment("Ibkr__AccountId", ibkrAccountId)
    .WithEnvironment("INFISICAL_CLIENT_ID", infisicalClientId)
    .WithEnvironment("INFISICAL_CLIENT_SECRET", infisicalClientSecret)
    .WithEnvironment("INFISICAL_PROJECT_ID", infisicalProjectId)
    .WithEnvironment("INFISICAL_SITE_URL", "https://eu.infisical.com")
    .WithEnvironment("INFISICAL_ENVIRONMENT", "dev")
    .WithEnvironment("INFISICAL_SECRET_PATH", "/app");

builder.Build().Run();
