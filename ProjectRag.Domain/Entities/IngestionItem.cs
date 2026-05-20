using ProjectRag.Domain.Enums;

namespace ProjectRag.Domain.Entities;

public sealed class IngestionItem
{
    public Guid Id { get; set; }
    public Guid? DocumentId { get; set; }
    public Guid IngestionRunId { get; set; }
    public string SourceUri { get; set; } = "";
    public IngestionItemStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public IngestionRun? IngestionRun { get; set; }
    public Document? Document { get; set; }
}
