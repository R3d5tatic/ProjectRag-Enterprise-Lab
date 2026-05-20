using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using ProjectRag.Application.Abstractions;
using ProjectRag.Application.Models;
using ProjectRag.Infrastructure.Options;

namespace ProjectRag.Infrastructure.Search;

internal sealed class ElasticSearchIndexService : ISearchIndexService
{
    private readonly ElasticsearchClient _client;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly ElasticsearchOptions _elasticsearchOptions;
    private readonly EmbeddingRuntimeOptions _embeddingOptions;

    public ElasticSearchIndexService(
        ElasticsearchClient client,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IOptions<ElasticsearchOptions> elasticsearchOptions,
        IOptions<EmbeddingRuntimeOptions> embeddingOptions)
    {
        _client = client;
        _embeddingGenerator = embeddingGenerator;
        _elasticsearchOptions = elasticsearchOptions.Value;
        _embeddingOptions = embeddingOptions.Value;
    }

    public async Task DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken)
    {
        await EnsureIndexExistsAsync(cancellationToken);

        var response = await _client.DeleteByQueryAsync<ElasticDocumentChunkRecord>(
            descriptor => descriptor
                .Indices(_elasticsearchOptions.IndexName)
                .Query(query => query
                    .Term(term => term
                        .Field(x => x.DocumentId)
                        .Value(documentId.ToString()))),
            cancellationToken);

        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException($"Failed to delete Elasticsearch chunks for document '{documentId}'.");
        }
    }

    public async Task<bool> DocumentHasIndexedChunksAsync(Guid documentId, CancellationToken cancellationToken)
    {
        await EnsureIndexExistsAsync(cancellationToken);

        var response = await _client.SearchAsync<ElasticDocumentChunkRecord>(
            descriptor => descriptor
                .Indices(_elasticsearchOptions.IndexName)
                .Size(1)
                .Query(query => query
                    .Term(term => term
                        .Field(x => x.DocumentId)
                        .Value(documentId.ToString()))),
            cancellationToken);

        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException($"Failed to check Elasticsearch chunks for document '{documentId}'.");
        }

        return response.Documents.Any();
    }

    public async Task UpsertChunksAsync(IReadOnlyCollection<SearchIndexChunk> chunks, CancellationToken cancellationToken)
    {
        if (chunks.Count == 0)
        {
            return;
        }

        await EnsureIndexExistsAsync(cancellationToken);

        var chunkList = chunks.ToList();
        var embeddings = await _embeddingGenerator.GenerateAsync(chunkList.Select(x => x.Text), cancellationToken: cancellationToken);

        for (int i = 0; i < chunkList.Count; i++)
        {
            var chunk = chunkList[i];
            var embedding = embeddings[i];

            if (embedding.Dimensions != ElasticDocumentChunkRecord.EmbeddingDimensions)
            {
                throw new InvalidOperationException($"Expected {ElasticDocumentChunkRecord.EmbeddingDimensions} embedding dimensions, but got {embedding.Dimensions}.");
            }

            var record = ElasticDocumentChunkRecordMapper.Map(
                chunk,
                embedding,
                _embeddingOptions.Model);

            var response = await _client.IndexAsync(
                record,
                descriptor => descriptor
                    .Index(_elasticsearchOptions.IndexName)
                    .Id(record.ChunkId),
                cancellationToken);

            if (!response.IsValidResponse)
            {
                throw new InvalidOperationException($"Failed to index chunk '{record.ChunkId}' in Elasticsearch.");
            }
        }
    }

    private async Task EnsureIndexExistsAsync(CancellationToken cancellationToken)
    {
        var existsResponse = await _client.Indices.ExistsAsync(_elasticsearchOptions.IndexName, cancellationToken);

        if (existsResponse.Exists)
        {
            return;
        }

        CreateIndexResponse createResponse;

        if (_elasticsearchOptions.EnableSemanticTextRetrieval)
        {
            createResponse = await CreateIndexWithSemanticTextAsync(cancellationToken);
        }
        else
        {
            createResponse = await CreateIndexWithoutSemanticTextAsync(cancellationToken);
        }

        if (!createResponse.IsValidResponse)
        {
            throw new InvalidOperationException($"Failed to create Elasticsearch index '{_elasticsearchOptions.IndexName}'.");
        }
    }

    private Task<CreateIndexResponse> CreateIndexWithoutSemanticTextAsync(CancellationToken cancellationToken)
    {
        return _client.Indices.CreateAsync<ElasticDocumentChunkRecord>(
            descriptor => descriptor
                .Index(_elasticsearchOptions.IndexName)
                .Mappings(mapping => mapping
                    .Properties(properties => properties
                        .Keyword(x => x.ChunkId)
                        .Keyword(x => x.DocumentId)
                        .Keyword(x => x.SourceUri)
                        .Keyword(x => x.SourceType)
                        .Text(x => x.Title)
                        .Text(x => x.Text)
                        .IntegerNumber(x => x.PageNumber)
                        .Text(x => x.SectionTitle)
                        .Keyword(x => x.Kind)
                        .Date(x => x.CreatedAt)
                        .Keyword(x => x.ChunkingStrategy)
                        .IntegerNumber(x => x.ChunkingMaxChunkSize)
                        .Keyword(x => x.EmbeddingModel)
                        .DenseVector(x => x.Embedding, vector => vector
                            .Dims(ElasticDocumentChunkRecord.EmbeddingDimensions)
                            .Similarity(DenseVectorSimilarity.Cosine)))),
            cancellationToken);
    }

    private Task<CreateIndexResponse> CreateIndexWithSemanticTextAsync(CancellationToken cancellationToken)
    {
        return _client.Indices.CreateAsync<ElasticDocumentChunkRecord>(
            descriptor => descriptor
                .Index(_elasticsearchOptions.IndexName)
                .Mappings(mapping => mapping
                    .Properties(properties => properties
                        .Keyword(x => x.ChunkId)
                        .Keyword(x => x.DocumentId)
                        .Keyword(x => x.SourceUri)
                        .Keyword(x => x.SourceType)
                        .Text(x => x.Title)
                        .Text(x => x.Text)
                        .SemanticText(x => x.SemanticText, semantic => semantic
                            .InferenceId(_elasticsearchOptions.SemanticTextInferenceId!))
                        .IntegerNumber(x => x.PageNumber)
                        .Text(x => x.SectionTitle)
                        .Keyword(x => x.Kind)
                        .Date(x => x.CreatedAt)
                        .Keyword(x => x.ChunkingStrategy)
                        .IntegerNumber(x => x.ChunkingMaxChunkSize)
                        .Keyword(x => x.EmbeddingModel)
                        .DenseVector(x => x.Embedding, vector => vector
                            .Dims(ElasticDocumentChunkRecord.EmbeddingDimensions)
                            .Similarity(DenseVectorSimilarity.Cosine)))),
            cancellationToken);
    }
}
