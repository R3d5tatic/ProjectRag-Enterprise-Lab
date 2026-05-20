namespace ProjectRag.Application.Models;

public sealed record IngestionItemResult(
    Guid IngestionItemId,
    Guid? DocumentId,
    string SourceUri,
    string Status,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt);
