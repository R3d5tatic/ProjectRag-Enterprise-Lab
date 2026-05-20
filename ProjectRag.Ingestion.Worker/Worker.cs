using Microsoft.Extensions.Options;
using ProjectRag.Application.Abstractions;
using ProjectRag.Ingestion.Worker.options;

namespace ProjectRag.Ingestion.Worker;

internal sealed class Worker(
    IServiceScopeFactory scopeFactory,
    ILogger<Worker> logger,
    IOptions<WorkerOptions> options) : BackgroundService
{
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(options.Value.PollIntervalSeconds);
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();

                var queue = scope.ServiceProvider.GetRequiredService<IPendingIngestionRunQueue>();
                var ingestionRuns = scope.ServiceProvider.GetRequiredService<IIngestionRunProcessor>();

                var runId = await queue.DequeuePendingRunAsync(stoppingToken);

                if (runId is null)
                {
                    await Task.Delay(_pollInterval, stoppingToken);
                    continue;
                }

                logger.LogInformation(
                    "Processing ingestion run {IngestionRunId}.",
                    runId);

                var result = await ingestionRuns.ProcessAsync(runId.Value, stoppingToken);

                logger.LogInformation(
                    "Processed ingestion run {IngestionRunId} with status {Status}. TotalItems={TotalItems}, CompletedItems={CompletedItems}, FailedItems={FailedItems}, SkippedItems={SkippedItems}.",
                    runId,
                    result?.Status ?? "Missing",
                    result?.Summary.TotalItems ?? 0,
                    result?.Summary.CompletedItems ?? 0,
                    result?.Summary.FailedItems ?? 0,
                    result?.Summary.SkippedItems ?? 0);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Ingestion worker failed while polling or processing pending runs.");
                await Task.Delay(_pollInterval, stoppingToken);
            }
        }
    }
}
