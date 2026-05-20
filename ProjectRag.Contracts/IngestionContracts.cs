namespace ProjectRag.Contracts;

public sealed record StartIngestionRequest(
    string SourcePath);

public sealed record IngestionRunResponse(
    Guid IngestionId,
    Guid KnowledgeBaseId,
    Guid? DataSourceId,
    string SourcePath,
    string Status,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    IngestionRunSummaryResponse Summary,
    IReadOnlyList<IngestionItemResponse> Items);

public sealed record IngestionRunSummaryResponse(
    int TotalItems,
    int CompletedItems,
    int FailedItems,
    int SkippedItems);

public sealed record IngestionItemResponse(
    Guid IngestionItemId,
    Guid? DocumentId,
    string SourceUri,
    string Status,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt);