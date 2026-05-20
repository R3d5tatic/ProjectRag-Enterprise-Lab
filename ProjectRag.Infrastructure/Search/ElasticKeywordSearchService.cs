using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Options;
using ProjectRag.Application.Abstractions;
using ProjectRag.Application.Models;
using ProjectRag.Application.Telemetry;
using ProjectRag.Domain.Enums;
using ProjectRag.Infrastructure.Options;

namespace ProjectRag.Infrastructure.Search;

internal sealed class ElasticKeywordSearchService : IKeywordSearchService
{
    private readonly ElasticsearchClient _client;
    private readonly ElasticsearchOptions _elasticSearchOptions;
    private readonly RetrievalOptions _retrievalOptions;

    public ElasticKeywordSearchService(
        ElasticsearchClient client,
        IOptions<ElasticsearchOptions> elasticSearchOptions,
        IOptions<RetrievalOptions> retrievalOptions)
    {
        _client = client;
        _elasticSearchOptions = elasticSearchOptions.Value;
        _retrievalOptions = retrievalOptions.Value;
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int topK, SearchFilters? filters, CancellationToken cancellationToken)
    {
        using var activity = ProjectRagTelemetry.ActivitySource.StartActivity("rag.search.keyword");
        activity?.SetTag("rag.query.length", query.Length);
        activity?.SetTag("rag.top_k", topK);
        activity?.SetTag("rag.filters.source_type", filters?.SourceType);

        if (string.IsNullOrWhiteSpace(query))
        {
            activity?.SetTag("rag.results.count", 0);
            return [];
        }

        topK = Math.Clamp(topK, 1, _retrievalOptions.MaxCandidateCount);
        activity?.SetTag("rag.top_k.effective", topK);

        var filterQueries = ElasticSearchFilterBuilder.Build(filters);

        var response = await _client.SearchAsync<ElasticDocumentChunkRecord>(
            descriptor => descriptor
                .Indices(_elasticSearchOptions.IndexName)
                .Size(topK)
                .Query(q => q
                    .Bool(b => b
                        .Must(must => must
                            .SimpleQueryString(sqs => sqs
                                .Query(query)
                                .Fields(new[]
                                {
                                    Infer.Field<ElasticDocumentChunkRecord>(x => x.Text),
                                    Infer.Field<ElasticDocumentChunkRecord>(x => x.SectionTitle),
                                    Infer.Field<ElasticDocumentChunkRecord>(x => x.Title),
                                })))
                        .Filter(filterQueries.ToArray()))),
            cancellationToken);

        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException("Elasticsearch keyword search failed.");
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
                    KeywordScore: hit.Score ?? 0,
                    MatchedBy: "keyword");
            })
            .ToList();

        activity?.SetTag("rag.results.count", results.Count);

        return results;
    }
}
