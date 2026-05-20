using ProjectRag.Application.Models;

namespace ProjectRag.Application.Abstractions;

public interface IIngestionRunProcessor
{
    Task<IngestionRunResult?> ProcessAsync(Guid ingestionRunId, CancellationToken cancellationToken);
}
