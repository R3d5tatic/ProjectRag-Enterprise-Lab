namespace ProjectRag.Domain.Entities;

public sealed class KnowledgeBase
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<DataSource> DataSources { get; set; } = [];
    public List<Document> Documents { get; set; } = [];
    public List<IngestionRun> IngestionRuns { get; set; } = [];
}
