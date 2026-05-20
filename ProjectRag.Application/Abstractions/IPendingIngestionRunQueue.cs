namespace ProjectRag.Application.Abstractions;

public interface IPendingIngestionRunQueue
{
    Task<Guid?> DequeuePendingRunAsync(CancellationToken cancellationToken);
}
