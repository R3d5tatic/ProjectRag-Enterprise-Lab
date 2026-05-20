using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using ProjectRag.Application.Abstractions;
using ProjectRag.Application.Models;
using ProjectRag.Application.Telemetry;
using ProjectRag.Domain.Enums;
using ProjectRag.Infrastructure.Options;

namespace ProjectRag.Infrastructure.Search;

internal sealed class ElasticVectorSearchService : IVectorSearchService
{
    private readonly ElasticsearchClient _client;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly ElasticsearchOptions _elasticsearchOptions;
    private readonly RetrievalOptions _retrievalOptions;
    private readonly EmbeddingRuntimeOptions _embeddingOptions;

    public ElasticVectorSearchService(
        ElasticsearchClient client,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IOptions<ElasticsearchOptions> elasticsearchOptions,
        IOptions<EmbeddingRuntimeOptions> embeddingOptions,
        IOptions<RetrievalOptions> retrievalOptions)
    {
        _client = client;
        _embeddingGenerator = embeddingGenerator;
        _elasticsearchOptions = elasticsearchOptions.Value;
        _embeddingOptions = embeddingOptions.Value;
        _retrievalOptions = retrievalOptions.Value;
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int topK, SearchFilters? filters, CancellationToken cancellationToken)
    {
        using var activity = ProjectRagTelemetry.ActivitySource.StartActivity("rag.search.vector");
        activity?.SetTag("rag.query.length", query.Length);
        activity?.SetTag("rag.top_k", topK);
        activity?.SetTag("rag.filters.source_type", filters?.SourceType);
        activity?.SetTag("rag.embedding.model", _embeddingOptions.Model);

        if (string.IsNullOrWhiteSpace(query))
        {
            activity?.SetTag("rag.results.count", 0);
            return [];
        }

        topK = Math.Clamp(topK, 1, _retrievalOptions.MaxCandidateCount);
        activity?.SetTag("rag.top_k.effective", topK);

        var queryEmbedding = await _embeddingGenerator.GenerateVectorAsync(query, cancellationToken: cancellationToken);

        var filterQueries = ElasticVectorSearchFilterBuilder.Build(filters, _embeddingOptions.Model);

        var response = await _client.SearchAsync<ElasticDocumentChunkRecord>(
            descriptor => descriptor
                .Indices(_elasticsearchOptions.IndexName)
                .Size(topK)
                .Knn(knn => knn
                    .Field(x => x.Embedding)
                    .QueryVector(queryEmbedding.ToArray())
                    .K(topK)
                    .NumCandidates(Math.Max(topK * 5, 50))
                    .Filter(filterQueries.ToArray())),
            cancellationToken);

        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException("Elasticsearch vector search failed.");
        }

        var results = response.Hits
            .Where(hit => hit.Source is not null)
            .Select(hit =>
            {
                var source = hit.Source!;

                return new SearchHit(
                    Guid.Parse(source.DocumentId),
                    Guid.Parse(source.ChunkId),
                    source.SourceUri,
                    source.Text,
                    RrfScore: 0,
                    source.PageNumber,
                    Enum.TryParse<ChunkKind>(source.Kind, out var kind) ? kind : ChunkKind.Unknown,
                    source.SectionTitle,
                    VectorScore: hit.Score ?? 0,
                    MatchedBy: "vector");
            })
            .ToList();

        activity?.SetTag("rag.results.count", results.Count);

        return results;
    }
}
