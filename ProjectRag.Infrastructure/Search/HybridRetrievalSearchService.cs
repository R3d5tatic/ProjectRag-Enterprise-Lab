using Microsoft.Extensions.Options;
using ProjectRag.Application.Abstractions;
using ProjectRag.Application.Models;
using ProjectRag.Application.Telemetry;
using ProjectRag.Infrastructure.Options;

namespace ProjectRag.Infrastructure.Search;

internal sealed class HybridRetrievalSearchService : IRetrievalSearchService
{
    private readonly RetrievalOptions _retrievalOptions;

    private readonly IVectorSearchService _vectorSearchService;
    private readonly IKeywordSearchService _keywordSearchService;
    private readonly IRankFusionService _rankFusionService;
    private readonly IRerankerService _rerankerService;

    public HybridRetrievalSearchService(
        IOptions<RetrievalOptions> retrievalOptions,
        IVectorSearchService vectorSearchService,
        IKeywordSearchService keywordSearchService,
        IRankFusionService rankFusionService,
        IRerankerService rerankerService
       )
    {
        _retrievalOptions = retrievalOptions.Value;
        _vectorSearchService = vectorSearchService;
        _keywordSearchService = keywordSearchService;
        _rankFusionService = rankFusionService;
        _rerankerService = rerankerService;
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(RetrievalQuery query, int topK, SearchFilters? filters, CancellationToken cancellationToken)
    {
        using var activity = ProjectRagTelemetry.ActivitySource.StartActivity("rag.retrieval.hybrid");
        activity?.SetTag("rag.query.length", query.OriginalQuery.Length);
        activity?.SetTag("rag.top_k", topK);
        activity?.SetTag("rag.filters.source_type", filters?.SourceType);
        activity?.SetTag("rag.retrieval.mode", _retrievalOptions.RetrievalMode);
        activity?.SetTag("rag.retrieval.fusion_mode", _retrievalOptions.FusionMode);
        activity?.SetTag("rag.retrieval.reranker_mode", GetEffectiveRerankerMode());
        activity?.SetTag("rag.retrieval.reranking_enabled", _retrievalOptions.EnableReranking);

        if (string.IsNullOrWhiteSpace(query.OriginalQuery)
            && string.IsNullOrWhiteSpace(query.SemanticQuery)
            && string.IsNullOrWhiteSpace(query.KeywordQuery))
        {
            return [];
        }

        topK = Math.Clamp(topK, 1, _retrievalOptions.MaxTopK);
        var candidateCount = Math.Clamp(
            Math.Max(topK, _retrievalOptions.CandidateCount),
            topK, _retrievalOptions.MaxCandidateCount);

        activity?.SetTag("rag.candidate_count", candidateCount);

        var vectorResultsTask = _vectorSearchService.SearchAsync(query.SemanticQuery, candidateCount, filters, cancellationToken);
        var keywordResultsTask = _keywordSearchService.SearchAsync(query.KeywordQuery, candidateCount, filters, cancellationToken);

        await Task.WhenAll(vectorResultsTask, keywordResultsTask);

        var vectorResults = await vectorResultsTask;
        var keywordResults = await keywordResultsTask;

        activity?.SetTag("rag.vector_results.count", vectorResults.Count);
        activity?.SetTag("rag.keyword_results.count", keywordResults.Count);

        var fusedCandidates = await _rankFusionService.FuseAsync(
            vectorResults,
            keywordResults,
            candidateCount,
            cancellationToken);

        activity?.SetTag("rag.fused_candidates.count", fusedCandidates.Count);
        activity?.SetTag("rag.reranking.enabled", _retrievalOptions.EnableReranking);

        if (!_retrievalOptions.EnableReranking)
        {
            var fusedResults = fusedCandidates
                .Take(topK)
                .ToList();

            activity?.SetTag("rag.results.count", fusedResults.Count);

            return fusedResults;
        }

        var results = await _rerankerService.RerankAsync(query, fusedCandidates, topK, cancellationToken);

        activity?.SetTag("rag.results.count", results.Count);

        return results;
    }

    private string GetEffectiveRerankerMode()
    {
        return _retrievalOptions.EnableReranking
            ? _retrievalOptions.RerankerMode
            : RetrievalStrategyNames.NoReranker;
    }
}
