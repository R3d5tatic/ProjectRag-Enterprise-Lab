using Microsoft.Extensions.AI;
using ProjectRag.Application.Models;
using ProjectRag.Domain.Enums;
using ProjectRag.Infrastructure.Options;
using ProjectRag.Infrastructure.Search;

namespace ProjectRag.Tests.Search;

public sealed class ElasticDocumentChunkRecordMapperTests
{
    [Fact]
    public void Map_sets_embedding_model_from_runtime_options_value()
    {
        var chunk = new SearchIndexChunk(
            DocumentId: Guid.NewGuid(),
            ChunkId: Guid.NewGuid(),
            SourceUri: "source.md",
            SourceType: "localFile",
            Title: "Source",
            Text: "Late payment policy",
            PageNumber: 1,
            SectionTitle: "Payments",
            Kind: ChunkKind.Paragraph,
            CreatedAt: DateTime.UtcNow,
            ChunkingStrategy: ChunkingStrategyNames.Paragraph,
            ChunkingMaxChunkSize: 1200);

        var embedding = new Embedding<float>(new float[ElasticDocumentChunkRecord.EmbeddingDimensions]);

        var record = ElasticDocumentChunkRecordMapper.Map(
            chunk,
            embedding,
            embeddingModel: "nomic-embed-text");

        Assert.Equal("nomic-embed-text", record.EmbeddingModel);
        Assert.Equal(embedding.Vector, record.Embedding);
        Assert.Equal(chunk.Text, record.SemanticText);
        Assert.Equal(ChunkingStrategyNames.Paragraph, record.ChunkingStrategy);
        Assert.Equal(1200, record.ChunkingMaxChunkSize);
    }
}
