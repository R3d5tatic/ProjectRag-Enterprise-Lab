using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using ProjectRag.Application.Abstractions;
using ProjectRag.Application.Models;
using ProjectRag.Application.Telemetry;
using ProjectRag.Domain.Enums;
using ProjectRag.Infrastructure.Options;

namespace ProjectRag.Infrastructure.Search;

internal sealed class ElasticNativeRrfSearchService : IRetrievalSearchService
{
    private readonly ElasticsearchClient _client;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly ElasticsearchOptions _elasticsearchOptions;
    private readonly EmbeddingRuntimeOptions _embeddingOptions;
    private readonly RetrievalOptions _retrievalOptions;
    private readonly IRerankerService _rerankerService;

    public ElasticNativeRrfSearchService(
         ElasticsearchClient client,
         IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
         IOptions<ElasticsearchOptions> elasticsearchOptions,
         IOptions<EmbeddingRuntimeOptions> embeddingOptions,
         IOptions<RetrievalOptions> retrievalOptions,
         IRerankerService rerankerService)
    {
        _client = client;
        _embeddingGenerator = embeddingGenerator;
        _elasticsearchOptions = elasticsearchOptions.Value;
        _embeddingOptions = embeddingOptions.Value;
        _retrievalOptions = retrievalOptions.Value;
        _rerankerService = rerankerService;
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(RetrievalQuery query, int topK, SearchFilters? filters, CancellationToken cancellationToken)
    {
        using var activity = ProjectRagTelemetry.ActivitySource.StartActivity("rag.retrieval.elastic_native_rrf");

        topK = Math.Clamp(topK, 1, _retrievalOptions.MaxTopK);

        var candidateCount = Math.Clamp(
            Math.Max(topK, _retrievalOptions.CandidateCount),
            topK,
            _retrievalOptions.MaxCandidateCount);

        activity?.SetTag("rag.retrieval.mode", _retrievalOptions.RetrievalMode);
        activity?.SetTag("rag.retrieval.fusion_mode", RetrievalStrategyNames.ElasticNativeRrf);
        activity?.SetTag("rag.retrieval.reranker_mode", GetEffectiveRerankerMode());
        activity?.SetTag("rag.retrieval.reranking_enabled", _retrievalOptions.EnableReranking);
        activity?.SetTag("rag.top_k", topK);
        activity?.SetTag("rag.candidate_count", candidateCount);
        activity?.SetTag("rag.retrieval.elastic.rrf_rank_window_size", GetElasticRrfRankWindowSize(candidateCount));
        activity?.SetTag("rag.retrieval.elastic.knn_num_candidates", GetElasticKnnNumCandidates(candidateCount));
        activity?.SetTag("rag.retrieval.elastic.semantic_text_enabled", _elasticsearchOptions.EnableSemanticTextRetrieval);

        if (string.IsNullOrWhiteSpace(query.SemanticQuery)
            && string.IsNullOrWhiteSpace(query.KeywordQuery))
        {
            return [];
        }

        var queryEmbedding = await _embeddingGenerator.GenerateVectorAsync(
            query.SemanticQuery,
            cancellationToken: cancellationToken);

        var keywordFilters = ElasticSearchFilterBuilder.Build(filters);
        var vectorFilters = ElasticVectorSearchFilterBuilder.Build(filters, _embeddingOptions.Model);

        var response = await _client.SearchAsync<ElasticDocumentChunkRecord>(
            s => s
                .Indices(_elasticsearchOptions.IndexName)
                .Size(candidateCount)
                .Retriever(r =>
                {
                    if (IsElasticSemanticRerankingEnabled())
                    {
                        r.TextSimilarityReranker(reranker => reranker
                            .Field("text")
                            .InferenceId(_retrievalOptions.ElasticRerankInferenceId!)
                            .InferenceText(query.OriginalQuery)
                            .RankWindowSize(GetElasticRrfRankWindowSize(candidateCount))
                            .Retriever(inner => inner
                                .Rrf(rrf => rrf
                                    .RankConstant(_retrievalOptions.RrfConstant)
                                    .RankWindowSize(GetElasticRrfRankWindowSize(candidateCount))
                                    .Retrievers(BuildRrfRetrievers(
                                        query,
                                        queryEmbedding.ToArray(),
                                        candidateCount,
                                        keywordFilters,
                                        vectorFilters)))));

                        return;
                    }

                    r.Rrf(rrf => rrf
                        .RankConstant(_retrievalOptions.RrfConstant)
                        .RankWindowSize(GetElasticRrfRankWindowSize(candidateCount))
                        .Retrievers(BuildRrfRetrievers(
                            query,
                            queryEmbedding.ToArray(),
                            candidateCount,
                            keywordFilters,
                            vectorFilters)));
                }),
            cancellationToken);

        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException("Elasticsearch native RRF search failed.");
        }

        var candidates = response.Hits
            .Where(hit => hit.Source is not null)
            .Select(hit => MapHit(hit.Source!, hit.Score ?? 0))
            .ToList();

        activity?.SetTag("rag.fused_candidates.count", candidates.Count);

        if (!ShouldApplyApplicationReranking())
        {
            var results = candidates
                .Take(topK)
                .ToList();

            activity?.SetTag("rag.results.count", results.Count);

            return results;
        }

        var rerankedResults = await _rerankerService.RerankAsync(
            query,
            candidates,
            topK,
            cancellationToken);

        activity?.SetTag("rag.results.count", rerankedResults.Count);

        return rerankedResults;
    }

    private int GetElasticKnnNumCandidates(int candidateCount)
    {
        return Math.Max(candidateCount * _retrievalOptions.ElasticKnnNumCandidatesMultiplier, _retrievalOptions.ElasticKnnMinNumCandidates);
    }

    private int GetElasticRrfRankWindowSize(int candidateCount)
    {
        return candidateCount * _retrievalOptions.ElasticRrfRankWindowMultiplier;
    }

    private bool IsElasticSemanticRerankingEnabled()
    {
        return _retrievalOptions.EnableReranking
            && _retrievalOptions.RerankerMode.Equals(RetrievalStrategyNames.ElasticSemanticReranker, StringComparison.OrdinalIgnoreCase);
    }

    private bool ShouldApplyApplicationReranking()
    {
        return _retrievalOptions.EnableReranking
            && _retrievalOptions.RerankerMode.Equals(RetrievalStrategyNames.LlmReranker, StringComparison.OrdinalIgnoreCase);
    }

    private Union<Retriever, RRFRetrieverComponent>[] BuildRrfRetrievers(
        RetrievalQuery query,
        float[] queryEmbedding,
        int candidateCount,
        ICollection<Query> keywordFilters,
        ICollection<Query> vectorFilters)
    {
        var retrievers = new List<Union<Retriever, RRFRetrieverComponent>>
        {

            BuildKeywordRetriever(query.KeywordQuery, keywordFilters),
            BuildVectorRetriever(
                queryEmbedding,
                candidateCount,
                GetElasticKnnNumCandidates(candidateCount),
                vectorFilters)
        };

        if (_elasticsearchOptions.EnableSemanticTextRetrieval)
        {
            retrievers.Add(BuildSemanticTextRetriever(query.SemanticQuery, keywordFilters));
        }

        return retrievers.ToArray();
    }

    private static Union<Retriever, RRFRetrieverComponent> BuildSemanticTextRetriever(string query, ICollection<Query> filters)
    {
        var retriever = new RetrieverDescriptor<ElasticDocumentChunkRecord>()
            .Standard(standard => standard
                .Query(q => q
                    .Bool(b => b
                        .Must(m => m
                            .Semantic(semantic => semantic
                                .Field("semanticText")
                                .Query(query)))
                        .Filter(filters.ToArray()))));

        return new Union<Retriever, RRFRetrieverComponent>(retriever);
    }

    private static Union<Retriever, RRFRetrieverComponent> BuildKeywordRetriever(string query, ICollection<Query> filters)
    {
        var retriever = new RetrieverDescriptor<ElasticDocumentChunkRecord>()
            .Standard(standard => standard
                .Query(q => q
                    .Bool(b => b
                        .Must(m => m
                            .SimpleQueryString(sqs => sqs
                                .Query(query)
                                .Fields(
                                    x => x.Text,
                                    x => x.SectionTitle,
                                    x => x.Title)))
                        .Filter(filters.ToArray()))));

        return new Union<Retriever, RRFRetrieverComponent>(retriever);
    }

    private static Union<Retriever, RRFRetrieverComponent> BuildVectorRetriever(
        float[] queryVector,
        int candidateCount,
        int numCandidates,
        ICollection<Query> filters)
    {
        var retriever = new RetrieverDescriptor<ElasticDocumentChunkRecord>()
            .Knn(knn => knn
                .Field("embedding")
                .QueryVector(queryVector)
                .K(candidateCount)
                .NumCandidates(numCandidates)
                .Filter(filters.ToArray()));

        return new Union<Retriever, RRFRetrieverComponent>(retriever);
    }

    private static SearchHit MapHit(ElasticDocumentChunkRecord source, double score)
    {
        return new SearchHit(
            Guid.Parse(source.DocumentId),
            Guid.Parse(source.ChunkId),
            source.SourceUri,
            source.Text,
            RrfScore: score,
            source.PageNumber,
            Enum.TryParse<ChunkKind>(source.Kind, out var kind) ? kind : ChunkKind.Unknown,
            source.SectionTitle,
            VectorScore: null,
            KeywordScore: null,
            MatchedBy: RetrievalStrategyNames.ElasticNativeRrf);
    }

    private string GetEffectiveRerankerMode()
    {
        return _retrievalOptions.EnableReranking ? _retrievalOptions.RerankerMode : RetrievalStrategyNames.NoReranker;
    }
}
