using Microsoft.Extensions.Options;
using ProjectRag.Application.Abstractions;
using ProjectRag.Application.Models;
using ProjectRag.Domain.Enums;
using ProjectRag.Infrastructure.Options;
using ProjectRag.Infrastructure.Search;

namespace ProjectRag.Tests.Search;

public sealed class HybridRetrievalSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_returns_empty_for_blank_query()
    {
        var service = new HybridRetrievalSearchService(
            Options.Create(new RetrievalOptions()),
            new StubVectorSearchService([]),
            new StubKeywordSearchService([]),
            CreateRankFusionService(),
            new StubRerankerService());

        var results = await service.SearchAsync(Query(""), topK: 5, filters: null, CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_includes_vector_only_hits()
    {
        var vectorHit = VectorHit("semantic late payment policy", score: 0.8);
        var service = new HybridRetrievalSearchService(
            Options.Create(new RetrievalOptions()),
            new StubVectorSearchService([vectorHit]),
            new StubKeywordSearchService([]),
            CreateRankFusionService(),
            new StubRerankerService());

        var results = await service.SearchAsync(
            Query("late payment"),
            topK: 5,
            filters: null,
            CancellationToken.None);

        var result = Assert.Single(results);

        Assert.Equal(vectorHit.ChunkId, result.ChunkId);

        Assert.Equal(1d / 61d, result.RrfScore);
        Assert.Equal("vector", result.MatchedBy);

        Assert.NotNull(result.VectorScore);
        Assert.Null(result.KeywordScore);
    }

    [Fact]
    public async Task SearchAsync_includes_keyword_only_hits()
    {
        var keywordHit = KeywordHit("exact keyword fee match", score: 12);
        var service = new HybridRetrievalSearchService(
            Options.Create(new RetrievalOptions()),
            new StubVectorSearchService([]),
            new StubKeywordSearchService([keywordHit]),
            CreateRankFusionService(),
            new StubRerankerService());

        var results = await service.SearchAsync(
            Query("fee"),
            topK: 5,
            filters: null,
            CancellationToken.None);

        var result = Assert.Single(results);

        Assert.Equal(keywordHit.ChunkId, result.ChunkId);

        Assert.Equal(1d / 61d, result.RrfScore);
        Assert.Equal("keyword", result.MatchedBy);

        Assert.Null(result.VectorScore);
        Assert.NotNull(result.KeywordScore);
    }

    [Fact]
    public async Task SearchAsync_deduplicates_same_chunk_from_both_searches()
    {
        var chunkId = Guid.NewGuid();
        var vectorHit = VectorHit("vector match", score: 0.8, chunkId);
        var keywordHit = KeywordHit("keyword match", score: 10, chunkId);
        var service = new HybridRetrievalSearchService(
            Options.Create(new RetrievalOptions()),
            new StubVectorSearchService([vectorHit]),
            new StubKeywordSearchService([keywordHit]),
            CreateRankFusionService(),
            new StubRerankerService());

        var results = await service.SearchAsync(
            Query("late payment"),
            topK: 5,
            filters: null,
            CancellationToken.None);

        var result = Assert.Single(results);

        Assert.Equal(chunkId, result.ChunkId);
    }

    [Fact]
    public async Task SearchAsync_fuses_chunk_matched_by_both_searches()
    {
        var hybridChunkId = Guid.NewGuid();
        var vectorOnly = VectorHit("vector only", score: 1);
        var keywordOnly = KeywordHit("keyword only", score: 1);
        var vectorHybrid = VectorHit("hybrid vector", score: 1, hybridChunkId);
        var keywordHybrid = KeywordHit("hybrid keyword", score: 1, hybridChunkId);

        var service = new HybridRetrievalSearchService(
            Options.Create(new RetrievalOptions()),
            new StubVectorSearchService([vectorOnly, vectorHybrid]),
            new StubKeywordSearchService([keywordOnly, keywordHybrid]),
            CreateRankFusionService(),
            new StubRerankerService());

        var results = await service.SearchAsync(
            Query("late payment"),
            topK: 5,
            filters: null,
            CancellationToken.None);

        Assert.Equal(hybridChunkId, results[0].ChunkId);
        Assert.True(results[0].RrfScore > results[1].RrfScore);
        Assert.Equal("hybrid", results[0].MatchedBy);

        Assert.NotNull(results[0].VectorScore);
        Assert.NotNull(results[0].KeywordScore);
    }

    [Fact]
    public async Task SearchAsync_reranks_fused_candidates_to_final_topK()
    {
        var reranker = new StubRerankerService();

        var service = new HybridRetrievalSearchService(
            Options.Create(new RetrievalOptions { CandidateCount = 30, MaxTopK = 20 }),
            new StubVectorSearchService(
            [
                VectorHit("vector one", score: 3),
                VectorHit("vector two", score: 2),
                VectorHit("vector three", score: 1)
            ]),
            new StubKeywordSearchService(
            [
                KeywordHit("keyword one", score: 3),
                KeywordHit("keyword two", score: 2),
                KeywordHit("keyword three", score: 1)
            ]),
            CreateRankFusionService(),
            reranker);

        var results = await service.SearchAsync(
            Query("late payment"),
            topK: 2,
            filters: null,
            CancellationToken.None);

        Assert.NotNull(reranker.ReceivedCandidates);
        Assert.NotEmpty(reranker.ReceivedCandidates);
        Assert.Equal(2, reranker.ReceivedTopK);

        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.NotNull(result.RerankScore));
    }

    [Fact]
    public async Task SearchAsync_caps_candidate_count_at_max_candidate_count()
    {
        var vectorSearch = new StubVectorSearchService([]);
        var keywordSearch = new StubKeywordSearchService([]);

        var service = new HybridRetrievalSearchService(
            Options.Create(new RetrievalOptions
            {
                CandidateCount = 50,
                MaxCandidateCount = 25,
                MaxTopK = 10
            }),
            vectorSearch,
            keywordSearch,
            CreateRankFusionService(),
            new StubRerankerService());

        await service.SearchAsync(
            Query("late payment"),
            topK: 5,
            filters: null,
            CancellationToken.None);

        Assert.Equal(25, vectorSearch.ReceivedTopK);
        Assert.Equal(25, keywordSearch.ReceivedTopK);
    }

    [Fact]
    public async Task SearchAsync_caps_final_topK_at_max_topK()
    {
        var reranker = new StubRerankerService();

        var service = new HybridRetrievalSearchService(
            Options.Create(new RetrievalOptions
            {
                CandidateCount = 30,
                MaxCandidateCount = 100,
                MaxTopK = 3
            }),
            new StubVectorSearchService(
            [
                VectorHit("vector one", score: 5),
                VectorHit("vector two", score: 4),
                VectorHit("vector three", score: 3),
                VectorHit("vector four", score: 2),
                VectorHit("vector five", score: 1)
            ]),
            new StubKeywordSearchService([]),
            CreateRankFusionService(),
            reranker);

        var results = await service.SearchAsync(
            Query("late payment"),
            topK: 10,
            filters: null,
            CancellationToken.None);

        Assert.Equal(3, reranker.ReceivedTopK);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task SearchAsync_does_not_call_reranker_when_reranking_is_disabled()
    {
        var reranker = new StubRerankerService();

        var service = new HybridRetrievalSearchService(
            Options.Create(new RetrievalOptions
            {
                EnableReranking = false,
                CandidateCount = 10,
                MaxCandidateCount = 20,
                MaxTopK = 10
            }),
            new StubVectorSearchService([VectorHit("vector one", score: 0.9)]),
            new StubKeywordSearchService([KeywordHit("keyword one", score: 12)]),
            CreateRankFusionService(),
            reranker);

        var results = await service.SearchAsync(
            Query("late payment"),
            topK: 2,
            filters: null,
            CancellationToken.None);

        Assert.Null(reranker.ReceivedQuery);
        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.Null(result.RerankScore));
    }

    [Fact]
    public async Task SearchAsync_passes_filters_to_vector_and_keyword_search()
    {
        var vectorSearch = new StubVectorSearchService([]);
        var keywordSearch = new StubKeywordSearchService([]);

        var service = new HybridRetrievalSearchService(
            Options.Create(new RetrievalOptions()),
            vectorSearch,
            keywordSearch,
            CreateRankFusionService(),
            new StubRerankerService());

        var filters = new SearchFilters(SourceType: "md");

        var query = new RetrievalQuery(
            OriginalQuery: "late payment",
            SemanticQuery: "semantic late payment",
            KeywordQuery: "\"late payment\" OR overdue");

        await service.SearchAsync(
            query,
            topK: 5,
            filters,
            CancellationToken.None);

        Assert.Same(filters, vectorSearch.ReceivedFilters);
        Assert.Same(filters, keywordSearch.ReceivedFilters);

        Assert.Equal("semantic late payment", vectorSearch.ReceivedQuery);
        Assert.Equal("\"late payment\" OR overdue", keywordSearch.ReceivedQuery);
    }

    private static SearchHit VectorHit(string text, double score, Guid? chunkId = null)
    {
        return new SearchHit(
            Guid.NewGuid(),
            chunkId ?? Guid.NewGuid(),
            "source.md",
            text,
            RrfScore: 0,
            null,
            ChunkKind.Paragraph,
            null,
            VectorScore: score,
            MatchedBy: "vector");
    }
    private static SearchHit KeywordHit(string text, double score, Guid? chunkId = null)
    {
        return new SearchHit(
            Guid.NewGuid(),
            chunkId ?? Guid.NewGuid(),
            "source.md",
            text,
            RrfScore: 0,
            null,
            ChunkKind.Paragraph,
            null,
            KeywordScore: score,
            MatchedBy: "keyword");
    }

    private sealed class StubVectorSearchService : IVectorSearchService
    {
        private readonly IReadOnlyList<SearchHit> _hits;
        public SearchFilters? ReceivedFilters { get; private set; }
        public string? ReceivedQuery { get; private set; }
        public int? ReceivedTopK { get; private set; }

        public StubVectorSearchService(IReadOnlyList<SearchHit> hits)
        {
            _hits = hits;
        }

        public Task<IReadOnlyList<SearchHit>> SearchAsync(
            string query,
            int topK,
            SearchFilters? filters,
            CancellationToken cancellationToken)
        {
            ReceivedQuery = query;
            ReceivedFilters = filters;
            ReceivedTopK = topK;
            return Task.FromResult(_hits.Take(topK).ToList() as IReadOnlyList<SearchHit>);
        }
    }

    private sealed class StubKeywordSearchService : IKeywordSearchService
    {
        private readonly IReadOnlyList<SearchHit> _hits;
        public SearchFilters? ReceivedFilters { get; private set; }
        public string? ReceivedQuery { get; private set; }
        public int? ReceivedTopK { get; private set; }

        public StubKeywordSearchService(IReadOnlyList<SearchHit> hits)
        {
            _hits = hits;
        }

        public Task<IReadOnlyList<SearchHit>> SearchAsync(
            string query,
            int topK,
            SearchFilters? filters,
            CancellationToken cancellationToken)
        {
            ReceivedFilters = filters;
            ReceivedQuery = query;
            ReceivedTopK = topK;
            return Task.FromResult(_hits.Take(topK).ToList() as IReadOnlyList<SearchHit>);
        }
    }

    private static RetrievalQuery Query(string query)
    {
        return new RetrievalQuery(
            OriginalQuery: query,
            SemanticQuery: query,
            KeywordQuery: query);
    }

    private static RrfRankFusionService CreateRankFusionService()
    {
        return new RrfRankFusionService(Options.Create(new RetrievalOptions()));
    }

    private sealed class StubRerankerService : IRerankerService
    {
        public RetrievalQuery? ReceivedQuery { get; private set; }
        public IReadOnlyList<SearchHit>? ReceivedCandidates { get; private set; }
        public int? ReceivedTopK { get; private set; }

        public Task<IReadOnlyList<SearchHit>> RerankAsync(RetrievalQuery query, IReadOnlyList<SearchHit> candidates, int topK, CancellationToken cancellationToken)
        {
            ReceivedQuery = query;
            ReceivedCandidates = candidates;
            ReceivedTopK = topK;

            return Task.FromResult(
                candidates
                    .Take(topK)
                    .Select((hit, index) => hit with { RerankScore = 1d - (index * 0.1d) })
                    .ToList() as IReadOnlyList<SearchHit>);
        }
    }
}
