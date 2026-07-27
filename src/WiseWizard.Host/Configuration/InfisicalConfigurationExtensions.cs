using System.Diagnostics.CodeAnalysis;
using Infisical.Sdk;
using Infisical.Sdk.Model;

namespace WiseWizard.Host.Configuration;

/// <summary>
/// Loads application secrets from Infisical into <see cref="IConfiguration"/> at startup, following
/// the helios convention: only the bootstrap machine-identity credentials
/// (<c>INFISICAL_CLIENT_ID</c> / <c>INFISICAL_CLIENT_SECRET</c> / <c>INFISICAL_PROJECT_ID</c>) live
/// in the cluster as env vars; every real secret is pulled from Infisical at runtime.
///
/// In development (no <c>INFISICAL_CLIENT_ID</c> present) this is a no-op — appsettings and
/// environment variables are the only configuration sources.
///
/// Secret keys use SCREAMING_SNAKE_CASE with <c>__</c> as the section separator (e.g.
/// <c>ANTHROPIC__APIKEY</c> maps to <c>Anthropic:ApiKey</c>); .NET configuration is case-insensitive.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Composition-root bootstrap; requires a live Infisical machine identity, exercised in staging/prod, not unit-tested.")]
public static class InfisicalConfigurationExtensions
{
    public static void AddEnvVariablesAndConfigureSecrets(this IHostApplicationBuilder builder)
    {
        // Environment variables are always a source (they also carry the Infisical bootstrap creds).
        builder.Configuration.AddEnvironmentVariables();

        var clientId = builder.Configuration["INFISICAL_CLIENT_ID"];
        var clientSecret = builder.Configuration["INFISICAL_CLIENT_SECRET"];
        var projectId = builder.Configuration["INFISICAL_PROJECT_ID"];

        // Development / local: no machine identity → env + appsettings only.
        if (string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(clientSecret)
            || string.IsNullOrWhiteSpace(projectId))
        {
            return;
        }

        var hostUri = builder.Configuration["INFISICAL_SITE_URL"] ?? "https://eu.infisical.com";
        var environment = builder.Configuration["INFISICAL_ENVIRONMENT"]
            ?? MapAspNetEnvironment(builder.Environment.EnvironmentName);
        var secretPath = builder.Configuration["INFISICAL_SECRET_PATH"] ?? "/app";

        var secrets = LoadSecrets(hostUri, clientId, clientSecret, projectId, environment, secretPath);
        builder.Configuration.AddInMemoryCollection(secrets);
    }

    private static Dictionary<string, string?> LoadSecrets(
        string hostUri, string clientId, string clientSecret,
        string projectId, string environment, string secretPath)
    {
        var settings = new InfisicalSdkSettingsBuilder().WithHostUri(hostUri).Build();
        var client = new InfisicalClient(settings);

        // Machine-identity (Universal Auth) login with the bootstrap credentials.
        client.Auth().UniversalAuth().LoginAsync(clientId, clientSecret).GetAwaiter().GetResult();

        var options = new ListSecretsOptions
        {
            ProjectId = projectId,
            EnvironmentSlug = environment,
            SecretPath = secretPath,
            Recursive = true,
            ExpandSecretReferences = true,
        };

        var secrets = client.Secrets().ListAsync(options).GetAwaiter().GetResult().ToList();
        
        Console.WriteLine($"[Infisical] Loaded {secrets.Count} secret(s) from {hostUri} / {environment} / {secretPath}");
        foreach (var secret in secrets)
        {
            Console.WriteLine($"  - {secret.SecretKey} -> {secret.SecretKey.Replace("__", ":")}");
        }

        // If CONNECTIONSTRINGS__WISEWIZARD not found in /app, also query root path
        if (!secrets.Any(s => s.SecretKey == "CONNECTIONSTRINGS__WISEWIZARD") && secretPath != "/")
        {
            Console.WriteLine($"[Infisical] CONNECTIONSTRINGS__WISEWIZARD not found in {secretPath}, querying root /");
            var rootOptions = new ListSecretsOptions
            {
                ProjectId = projectId,
                EnvironmentSlug = environment,
                SecretPath = "/",
                Recursive = true,
                ExpandSecretReferences = true,
            };
            var rootSecrets = client.Secrets().ListAsync(rootOptions).GetAwaiter().GetResult();
            Console.WriteLine($"[Infisical] Loaded {rootSecrets.Count()} secret(s) from root /");
            foreach (var rootSecret in rootSecrets)
            {
                Console.WriteLine($"  - {rootSecret.SecretKey}");
            }
            
            // Add root secrets only if not already present (avoid duplicates, prefer /app over /)
            foreach (var rootSecret in rootSecrets)
            {
                if (!secrets.Any(s => s.SecretKey == rootSecret.SecretKey))
                {
                    secrets.Add(rootSecret);
                }
            }
        }

        // Map SCREAMING_SNAKE_CASE keys with '__' section separators to .NET's ':' convention.
        // Handle both single underscore (CONNECTIONSTRINGS_WISEWIZARD) and double underscore (CONNECTIONSTRINGS__WISEWIZARD)
        return secrets.ToDictionary(
            s => s.SecretKey.Replace("__", ":").Replace("_", ":"),
            s => (string?)s.SecretValue);
    }

    private static string MapAspNetEnvironment(string aspNetEnvironment) => aspNetEnvironment switch
    {
        "Production" => "production",
        "Staging" => "staging",
        _ => "dev",
    };
}
