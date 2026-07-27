using WiseWizard.Core.Services;

namespace WiseWizard.Host.HostedServices;

/// <summary>
/// Periodically pings the Brokerage session to keep it alive and detect lapses, delegating the
/// domain logic to <see cref="KeepAliveService"/>. Runs on the configured interval (default 60s).
/// </summary>
public sealed class BrokerKeepAliveService(
    IServiceScopeFactory scopeFactory,
    ILogger<BrokerKeepAliveService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var keepAlive = scope.ServiceProvider.GetRequiredService<KeepAliveService>();
                await keepAlive.TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Broker keep-alive tick failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
