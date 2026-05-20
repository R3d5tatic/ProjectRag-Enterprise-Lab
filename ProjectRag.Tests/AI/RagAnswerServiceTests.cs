using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using ProjectRag.Application.Abstractions;
using ProjectRag.Application.Models;
using ProjectRag.Domain.Enums;
using ProjectRag.Infrastructure.AI;
using ProjectRag.Infrastructure.Options;
using ProjectRag.Tests.Support;

namespace ProjectRag.Tests.AI;

public sealed class RagAnswerServiceTests
{
    [Fact]
    public async Task AnswerAsync_returns_answered_claims_and_citations_for_supported_response()
    {
        var hit = Hit("Late balances may receive a monthly fee after a grace period.", rerankScore: 0.95);

        var service = CreateService(
            [hit],
            """
            {
              "answerStatus": "answered",
              "answer": "Late balances may receive a monthly fee after a grace period.",
              "claims": [
                {
                  "text": "Late balances may receive a monthly fee after a grace period.",
                  "sourceIndexes": [1]
                }
              ]
            }
            """);

        var result = await service.AnswerAsync(
            "What are the late payment fees?",
            topK: 5,
            filters: null,
            CancellationToken.None);

        Assert.Equal("answered", result.AnswerStatus);
        Assert.Contains("monthly fee", result.Answer);

        var claim = Assert.Single(result.Claims);
        Assert.Contains("monthly fee", claim.Text);
        Assert.Contains(hit.ChunkId, claim.CitationChunkIds);

        var citation = Assert.Single(result.Citations);
        Assert.Equal(hit.ChunkId, citation.ChunkId);

        Assert.Equal(5, result.RetrievalDiagnostics.RequestedTopK);
        Assert.Equal(30, result.RetrievalDiagnostics.CandidateCount);
        Assert.Equal(1, result.RetrievalDiagnostics.ReturnedContextCount);
        Assert.True(result.RetrievalDiagnostics.RerankingApplied);
        AssertRetrievalStrategy(result.RetrievalDiagnostics);

        Assert.Equal("Ollama", result.ModelInfo.ChatProvider);
        Assert.Equal("llama3.2", result.ModelInfo.ChatModel);
        Assert.Equal("Ollama", result.ModelInfo.EmbeddingProvider);
        Assert.Equal("nomic-embed-text", result.ModelInfo.EmbeddingModel);
    }

    [Fact]
    public async Task AnswerAsync_returns_insufficient_context_when_model_json_is_invalid()
    {
        var service = CreateService(
            [Hit("Late balances may receive a monthly fee after a grace period.")],
            "not json");

        var result = await service.AnswerAsync(
            "What are the late payment fees?",
            topK: 5,
            filters: null,
            CancellationToken.None);

        Assert.Equal("insufficientContext", result.AnswerStatus);
        Assert.Contains("not have enough information", result.Answer);
        Assert.Empty(result.Claims);

        Assert.Single(result.Citations);
        Assert.Equal(30, result.RetrievalDiagnostics.CandidateCount);
        Assert.Equal(1, result.RetrievalDiagnostics.ReturnedContextCount);
        AssertRetrievalStrategy(result.RetrievalDiagnostics);
    }

    [Fact]
    public async Task AnswerAsync_returns_insufficient_context_when_model_omits_claim_citations()
    {
        var service = CreateService(
            [Hit("Late balances may receive a monthly fee after a grace period.")],
            """
            {
              "answerStatus": "answered",
              "answer": "Late balances may receive a monthly fee after a grace period.",
              "claims": [
                {
                  "text": "Late balances may receive a monthly fee after a grace period.",
                  "sourceIndexes": []
                }
              ]
            }
            """);

        var result = await service.AnswerAsync(
            "What are the late payment fees?",
            topK: 5,
            filters: null,
            CancellationToken.None);

        Assert.Equal("insufficientContext", result.AnswerStatus);
        Assert.Contains("not have enough information", result.Answer);
        Assert.Empty(result.Claims);

        Assert.Single(result.Citations);
        Assert.Equal(30, result.RetrievalDiagnostics.CandidateCount);
        Assert.Equal(1, result.RetrievalDiagnostics.ReturnedContextCount);
        AssertRetrievalStrategy(result.RetrievalDiagnostics);
    }

    [Fact]
    public async Task AnswerAsync_returns_insufficient_context_without_calling_chat_when_retrieval_has_no_hits()
    {
        var chatClient = new CountingChatClient();

        var service = new RagAnswerService(
            Options.Create(new ChatRuntimeOptions
            {
                Provider = "Ollama",
                Model = "llama3.2"
            }),
            Options.Create(new EmbeddingRuntimeOptions
            {
                Provider = "Ollama",
                Model = "nomic-embed-text"
            }),
            Options.Create(new RetrievalOptions()),
            new StubRetrievalSearchService([]),
            chatClient,
            new StubQueryRewriteService());

        var result = await service.AnswerAsync(
            "What are the late payment fees?",
            topK: 5,
            filters: null,
            CancellationToken.None);

        Assert.Equal("insufficientContext", result.AnswerStatus);
        Assert.Contains("not have enough information", result.Answer);
        Assert.Empty(result.Claims);
        Assert.Empty(result.Citations);

        Assert.Equal(30, result.RetrievalDiagnostics.CandidateCount);
        Assert.Equal(0, result.RetrievalDiagnostics.ReturnedContextCount);
        Assert.False(result.RetrievalDiagnostics.RerankingApplied);
        AssertRetrievalStrategy(result.RetrievalDiagnostics);
        Assert.Equal(0, chatClient.CallCount);
    }

    [Fact]
    public async Task AnswerAsync_returns_retrieval_strategy_from_options()
    {
        var service = CreateService(
            [Hit("Late balances may receive a monthly fee after a grace period.", rerankScore: 0.95)],
            """
            {
              "answerStatus": "answered",
              "answer": "Late balances may receive a monthly fee after a grace period.",
              "claims": [
                {
                  "text": "Late balances may receive a monthly fee after a grace period.",
                  "sourceIndexes": [1]
                }
              ]
            }
            """,
            new RetrievalOptions
            {
                CandidateCount = 42,
                RetrievalMode = "semantic",
                FusionMode = RetrievalStrategyNames.ElasticNativeRrf,
                RerankerMode = RetrievalStrategyNames.NoReranker,
                RrfConstant = 99
            });

        var result = await service.AnswerAsync(
            "What are the late payment fees?",
            topK: 5,
            filters: null,
            CancellationToken.None);

        Assert.Equal(42, result.RetrievalDiagnostics.CandidateCount);
        Assert.Equal("semantic", result.RetrievalDiagnostics.RetrievalMode);
        Assert.Equal(RetrievalStrategyNames.ElasticNativeRrf, result.RetrievalDiagnostics.FusionMode);
        Assert.Equal(RetrievalStrategyNames.NoReranker, result.RetrievalDiagnostics.RerankerMode);
        Assert.Equal(99, result.RetrievalDiagnostics.RrfConstant);
    }

    [Fact]
    public async Task AnswerAsync_reports_none_reranker_mode_when_reranking_is_disabled()
    {
        var service = CreateService(
            [Hit("Late balances may receive a monthly fee after a grace period.")],
            """
            {
              "answerStatus": "answered",
              "answer": "Late balances may receive a monthly fee after a grace period.",
              "claims": [
                {
                  "text": "Late balances may receive a monthly fee after a grace period.",
                  "sourceIndexes": [1]
                }
              ]
            }
            """,
            new RetrievalOptions
            {
                EnableReranking = false,
                RerankerMode = RetrievalStrategyNames.LlmReranker
            });

        var result = await service.AnswerAsync(
            "What are the late payment fees?",
            topK: 5,
            filters: null,
            CancellationToken.None);

        Assert.False(result.RetrievalDiagnostics.RerankingApplied);
        Assert.Equal(RetrievalStrategyNames.NoReranker, result.RetrievalDiagnostics.RerankerMode);
    }

    private static RagAnswerService CreateService(
        IReadOnlyList<SearchHit> hits,
        string chatResponse,
        RetrievalOptions? retrievalOptions = null)
    {
        return new RagAnswerService(
            Options.Create(new ChatRuntimeOptions
            {
                Provider = "Ollama",
                Model = "llama3.2"
            }),
            Options.Create(new EmbeddingRuntimeOptions
            {
                Provider = "Ollama",
                Model = "nomic-embed-text"
            }),
            Options.Create(retrievalOptions ?? new RetrievalOptions()),
            new StubRetrievalSearchService(hits),
            new FakeChatClient(chatResponse),
            new StubQueryRewriteService());
    }

    private static void AssertRetrievalStrategy(RetrievalDiagnostics diagnostics)
    {
        Assert.Equal(RetrievalStrategyNames.Hybrid, diagnostics.RetrievalMode);
        Assert.Equal(RetrievalStrategyNames.ApplicationRrf, diagnostics.FusionMode);
        Assert.Equal(RetrievalStrategyNames.LlmReranker, diagnostics.RerankerMode);
        Assert.Equal(60, diagnostics.RrfConstant);
    }

    private static SearchHit Hit(string text, double? rerankScore = null)
    {
        return new SearchHit(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "source.md",
            text,
            RrfScore: 1d / 61d,
            PageNumber: null,
            ChunkKind.Paragraph,
            SectionTitle: "Late Payment Policy",
            VectorScore: 0.9,
            KeywordScore: 12,
            MatchedBy: "hybrid",
            RerankScore: rerankScore);
    }

    private sealed class StubRetrievalSearchService : IRetrievalSearchService
    {
        private readonly IReadOnlyList<SearchHit> _hits;

        public StubRetrievalSearchService(IReadOnlyList<SearchHit> hits)
        {
            _hits = hits;
        }

        public Task<IReadOnlyList<SearchHit>> SearchAsync(
            RetrievalQuery query,
            int topK,
            SearchFilters? filters,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_hits.Take(topK).ToList() as IReadOnlyList<SearchHit>);
        }
    }

    private sealed class StubQueryRewriteService : IQueryRewriteService
    {
        public Task<QueryRewrite> RewriteAsync(string query, CancellationToken cancellationToken)
        {
            return Task.FromResult(new QueryRewrite(
                OriginalQuery: query,
                SemanticQuery: query,
                KeywordQuery: query,
                Status: "test-fake"));
        }
    }

    private sealed class CountingChatClient : IChatClient
    {
        public int CallCount { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "{}")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return AsyncEnumerable.Empty<ChatResponseUpdate>();
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            return null;
        }

        public void Dispose()
        {
        }
    }
}
