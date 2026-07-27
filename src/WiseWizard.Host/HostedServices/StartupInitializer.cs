using Hangfire;
using WiseWizard.Host.Jobs;
using WiseWizard.Infrastructure.Persistence;

namespace WiseWizard.Host.HostedServices;

/// <summary>
/// Runs once at startup: creates the domain schema if needed, registers the recurring Hangfire jobs
/// (nightly Run at 23:00, batch poll every 5 minutes), and enqueues a one-off resume of any Run
/// left in-flight by a restart (AC-08).
/// </summary>
public sealed class StartupInitializer(
    IDbConnectionFactory connectionFactory,
    IRecurringJobManager recurringJobs,
    IBackgroundJobClient backgroundJobs,
    ILogger<StartupInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await SchemaInitializer.InitializeAsync(connectionFactory, cancellationToken);
        logger.LogInformation("Domain schema initialized");

        // Nightly Run at 23:00 local time.
        recurringJobs.AddOrUpdate<NightlyPipelineJob>(
            "nightly-run",
            job => job.StartAsync(CancellationToken.None),
            "0 23 * * *");

        // Advance any in-flight Run's Batch jobs every 5 minutes.
        recurringJobs.AddOrUpdate<NightlyPipelineJob>(
            "nightly-poll",
            job => job.PollAsync(CancellationToken.None),
            "*/5 * * * *");

        // Resume Runs interrupted by a restart, once, now.
        backgroundJobs.Enqueue<NightlyPipelineJob>(job => job.ResumeAsync(CancellationToken.None));

        logger.LogInformation("Recurring jobs registered");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
