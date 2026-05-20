using ProjectRag.Domain.Enums;

namespace ProjectRag.Application.Models;

public sealed record SearchIndexChunk(
    Guid DocumentId,
    Guid ChunkId,
    string SourceUri,
    string? SourceType,
    string Title,
    string Text,
    int? PageNumber,
    string? SectionTitle,
    ChunkKind Kind,
    DateTime CreatedAt,
    string ChunkingStrategy,
    int ChunkingMaxChunkSize);