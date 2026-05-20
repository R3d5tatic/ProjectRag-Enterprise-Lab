using Microsoft.Extensions.AI;
using ProjectRag.Application.Models;

namespace ProjectRag.Infrastructure.Search;

internal static class ElasticDocumentChunkRecordMapper
{
    public static ElasticDocumentChunkRecord Map(
        SearchIndexChunk chunk,
        Embedding<float> embedding,
        string embeddingModel)
    {
        return new ElasticDocumentChunkRecord
        {
            ChunkId = chunk.ChunkId.ToString(),
            DocumentId = chunk.DocumentId.ToString(),
            SourceUri = chunk.SourceUri,
            SourceType = chunk.SourceType,
            Title = chunk.Title,
            Text = chunk.Text,
            SemanticText = chunk.Text,
            PageNumber = chunk.PageNumber,
            SectionTitle = chunk.SectionTitle,
            Kind = chunk.Kind.ToString(),
            CreatedAt = chunk.CreatedAt,
            ChunkingStrategy = chunk.ChunkingStrategy,
            ChunkingMaxChunkSize = chunk.ChunkingMaxChunkSize,
            EmbeddingModel = embeddingModel,
            Embedding = embedding.Vector
        };
    }
}
