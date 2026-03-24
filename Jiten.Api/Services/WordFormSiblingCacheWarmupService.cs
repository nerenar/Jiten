namespace Jiten.Api.Services;

public class WordFormSiblingCacheWarmupService(IServiceProvider services, ILogger<WordFormSiblingCacheWarmupService> logger)
    : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _ = services.GetRequiredService<IWordFormSiblingCache>();
            logger.LogInformation("WordFormSiblingCache warmup completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WordFormSiblingCache warmup failed");
        }

        return Task.CompletedTask;
    }
}
