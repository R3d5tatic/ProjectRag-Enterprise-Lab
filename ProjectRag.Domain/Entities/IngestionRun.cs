using ProjectRag.Domain.Enums;

namespace ProjectRag.Domain.Entities;

public sealed class IngestionRun
{
    public Guid Id { get; set; }
    public Guid KnowledgeBaseId { get; set; }
    public Guid DataSourceId { get; set; }
    public string SourcePath { get; set; } = "";
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public IngestionRunStatus Status { get; set; }
    public List<IngestionItem> Items { get; set; } = [];
    public KnowledgeBase? KnowledgeBase { get; set; }
    public DataSource? DataSource { get; set; }
}
