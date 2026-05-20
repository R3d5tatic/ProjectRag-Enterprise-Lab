using Microsoft.Extensions.Options;
using ProjectRag.Application.Abstractions;
using ProjectRag.Application.Models;
using ProjectRag.Application.Telemetry;
using ProjectRag.Infrastructure.Options;

namespace ProjectRag.Infrastructure.Search;

internal sealed class RrfRankFusionService : IRankFusionService
{
    private readonly RetrievalOptions _retrievalOptions;

    public RrfRankFusionService(IOptions<RetrievalOptions> retrievalOptions)
    {
        _retrievalOptions = retrievalOptions.Value;
    }

    public Task<IReadOnlyList<SearchHit>> FuseAsync(IReadOnlyList<SearchHit> vectorResults, IReadOnlyList<SearchHit> keywordResults, int topK, CancellationToken cancellationToken)
    {
        using var activity = ProjectRagTelemetry.ActivitySource.StartActivity("rag.rank_fusion.rrf");
        activity?.SetTag("rag.vector_results.count", vectorResults.Count);
        activity?.SetTag("rag.keyword_results.count", keywordResults.Count);
        activity?.SetTag("rag.top_k", topK);
        activity?.SetTag("rag.rrf.k", _retrievalOptions.RrfConstant);

        topK = Math.Max(topK, 1);
        activity?.SetTag("rag.top_k.effective", topK);

        var candidates = new Dictionary<Guid, FusionCandidate>();

        AddResults(candidates, vectorResults, RetrievalSource.Vector, _retrievalOptions.RrfConstant);
        AddResults(candidates, keywordResults, RetrievalSource.Keyword, _retrievalOptions.RrfConstant);

        var results = candidates.Values
            .Select(candidate =>
            {
                var matchedBy = candidate.HasVectorScore && candidate.HasKeywordScore ? "hybrid" : candidate.HasVectorScore ? "vector" : "keyword";

                return candidate.RepresentativeHit with
                {
                    RrfScore = candidate.RrfScore,
                    VectorScore = candidate.VectorScore,
                    KeywordScore = candidate.KeywordScore,
                    MatchedBy = matchedBy,
                };
            })
            .OrderByDescending(x => x.RrfScore)
            .Take(topK)
            .ToList();

        activity?.SetTag("rag.results.count", results.Count);

        return Task.FromResult(results as IReadOnlyList<SearchHit>);
    }

    private static void AddResults(
        Dictionary<Guid, FusionCandidate> candidates,
        IReadOnlyList<SearchHit> hits,
        RetrievalSource source,
        int rrfConstant)
    {
        for (int index = 0; index < hits.Count; index++)
        {
            var hit = hits[index];
            var rank = index + 1;
            var rrfContribution = 1d / (rrfConstant + rank);

            if (!candidates.TryGetValue(hit.ChunkId, out var candidate))
            {
                candidate = new FusionCandidate(hit);
                candidates[hit.ChunkId] = candidate;
            }

            candidate.RrfScore += rrfContribution;

            if (source == RetrievalSource.Vector)
            {
                candidate.VectorScore = hit.VectorScore;
                candidate.HasVectorScore = true;
            }
            else
            {
                candidate.KeywordScore = hit.KeywordScore;
                candidate.HasKeywordScore = true;
            }
        }
    }

    private sealed class FusionCandidate
    {
        public SearchHit RepresentativeHit { get; }
        public FusionCandidate(SearchHit representativeHit)
        {
            RepresentativeHit = representativeHit;
        }

        public double RrfScore { get; set; }
        public double? VectorScore { get; set; }
        public double? KeywordScore { get; set; }
        public bool HasVectorScore { get; set; }
        public bool HasKeywordScore { get; set; }
    }

    private enum RetrievalSource
    {
        Vector,
        Keyword,
    }
}
