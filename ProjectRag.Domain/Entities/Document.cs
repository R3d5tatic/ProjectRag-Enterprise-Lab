namespace ProjectRag.Domain.Entities;

public sealed class Document
{
    public Guid Id { get; set; }
    public Guid KnowledgeBaseId { get; set; }
    public Guid? DataSourceId { get; set; }
    public string SourceUri { get; set; } = "";
    public string Title { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public string? SourceType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<DocumentChunk> Chunks { get; set; } = [];
    public KnowledgeBase? KnowledgeBase { get; set; }
    public DataSource? DataSource { get; set; }
}