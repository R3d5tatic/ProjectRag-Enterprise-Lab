using ProjectRag.Domain.Enums;

namespace ProjectRag.Domain.Entities;

public sealed class DocumentChunk
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string Text { get; set; } = "";
    public int? PageNumber { get; set; }
    public string? SectionTitle { get; set; }
    public string? LayoutRole { get; set; }
    public string? BoundingRegionsJson { get; set; }
    public ChunkKind Kind { get; set; }
    public DateTime CreatedAt { get; set; }
    public Document? Document { get; set; }
}
