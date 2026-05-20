namespace ProjectRag.Domain.Entities;

public sealed class DataSource
{
    public Guid Id { get; set; }
    public Guid KnowledgeBaseId { get; set; }
    public string Name { get; set; } = "";
    public string SourceType { get; set; } = "";
    public string SourceUri { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public KnowledgeBase? KnowledgeBase { get; set; }
    public List<Document> Documents { get; set; } = [];
    public List<IngestionRun> IngestionRuns { get; set; } = [];
}
