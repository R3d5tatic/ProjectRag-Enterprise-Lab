namespace ProjectRag.Application.Models;

public sealed record IngestionRunSummary(
    int TotalItems,
    int CompletedItems,
    int FailedItems,
    int SkippedItems);