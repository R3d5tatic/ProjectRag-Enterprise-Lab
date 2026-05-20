namespace ProjectRag.Application.Models;

public sealed record IngestionRunResult(
    Guid IngestionId,
    Guid KnowledgeBaseId,
    Guid? DataSourceId,
    string SourcePath,
    string Status,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    IngestionRunSummary Summary,
    IReadOnlyList<IngestionItemResult> Items);