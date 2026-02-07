using Jiten.Core;
using Microsoft.EntityFrameworkCore;

namespace Jiten.Api.Services;

public class ParserWarmupService(IDbContextFactory<JitenDbContext> contextFactory, ILogger<ParserWarmupService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Parser warmup starting");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await Parser.Parser.WarmupAsync(contextFactory);
            sw.Stop();
            logger.LogInformation("Parser warmup completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Parser warmup failed");
        }
    }
}
