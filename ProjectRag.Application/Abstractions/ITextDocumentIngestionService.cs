namespace ProjectRag.Application.Abstractions;

public interface ITextDocumentIngestionService
{
    Task IngestPathAsync(
        Guid ingestionRunId,
        Guid knowledgeBaseId,
        Guid? dataSourceId,
        string sourcePath,
        CancellationToken cancellationToken);
}
