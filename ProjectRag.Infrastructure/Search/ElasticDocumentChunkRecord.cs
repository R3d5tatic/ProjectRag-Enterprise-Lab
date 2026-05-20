namespace ProjectRag.Infrastructure.Search;

internal sealed class ElasticDocumentChunkRecord
{
    public const int EmbeddingDimensions = 768;
    public string ChunkId { get; set; } = "";
    public string DocumentId { get; set; } = "";
    public string SourceUri { get; set; } = "";
    public string? SourceType { get; set; }
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string SemanticText { get; set; } = "";
    public int? PageNumber { get; set; }
    public string? SectionTitle { get; set; }
    public string Kind { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string ChunkingStrategy { get; set; } = "";
    public int ChunkingMaxChunkSize { get; set; }
    public string EmbeddingModel { get; set; } = "";
    public ReadOnlyMemory<float> Embedding { get; set; }
}
