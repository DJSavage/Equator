using Kelvinvale.Core.Application;

namespace Kelvinvale.Api.Infrastructure;

/// <summary>
/// Runs the settlement pass on a timer. The work itself lives in <see cref="ISettlementRunner"/> so
/// tests can drive it deterministically without the timer.
/// </summary>
public sealed class SettlementBackgroundService(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<SettlementBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<ISettlementRunner>();
                await runner.RunDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Settlement pass failed; will retry on the next tick.");
            }
        }
    }
}
