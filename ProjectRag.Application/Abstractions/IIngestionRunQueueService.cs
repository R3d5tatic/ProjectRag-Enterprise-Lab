using ProjectRag.Application.Models;

namespace ProjectRag.Application.Abstractions;

public interface IIngestionRunQueueService
{
    Task<IngestionRunResult> QueueAsync(string sourcePath, CancellationToken cancellationToken);
    Task<IngestionRunResult?> GetAsync(Guid ingestionRunId, CancellationToken cancellationToken);
}