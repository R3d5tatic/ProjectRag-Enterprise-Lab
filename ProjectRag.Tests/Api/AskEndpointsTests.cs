using ProjectRag.Contracts;
using ProjectRag.Tests.Support;
using ProjectRag.Application.Abstractions;
using ProjectRag.Application.Models;
using ProjectRag.Domain.Enums;
using RetrievalStrategyNames = ProjectRag.Infrastructure.Options.RetrievalStrategyNames;
using System.Net;
using System.Net.Http.Json;

namespace ProjectRag.Tests.Api;

public sealed class AskEndpointsTests : IClassFixture<RagApiFactory>
{
    private readonly HttpClient _client;
    private readonly RagApiFactory _factory;

    public AskEndpointsTests(RagApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Ask_returns_answer_with_citations_for_ingested_documents()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("projectrag-ask-test-");

        try
        {
            var filePath = Path.Combine(tempDirectory.FullName, "late-payment-policy.md");

            await File.WriteAllTextAsync(filePath, """
                # Late Payment Policy

                Late balances may receive a monthly fee after a grace period.
                """);

            var ingestionResponse = await _client.PostAsJsonAsync(
                "/api/v1/ingestions",
                new StartIngestionRequest(filePath));

            var ingestion = await IngestionApiTestHelper.ReadQueuedIngestionAsync(ingestionResponse);
            await IngestionApiTestHelper.ProcessQueuedIngestionAsync(_factory, ingestion);

            var askResponse = await _client.PostAsJsonAsync(
                "/api/v1/ask",
                new AskRequest("What are the late payment fees?", TopK: 5));

            Assert.Equal(HttpStatusCode.OK, askResponse.StatusCode);

            var body = await askResponse.Content.ReadFromJsonAsync<AskResponse>();

            Assert.NotNull(body);

            Assert.Equal("answered", body.AnswerStatus);
            Assert.Contains("monthly fee", body.Answer);

            Assert.NotNull(body.QueryRewrite);
            Assert.Equal("What are the late payment fees?", body.QueryRewrite.OriginalQuery);
            Assert.Equal("What are the late payment fees?", body.QueryRewrite.SemanticQuery);
            Assert.Equal("What are the late payment fees?", body.QueryRewrite.KeywordQuery);
            Assert.Equal("test-fake", body.QueryRewrite.Status);

            Assert.NotEmpty(body.Claims);
            Assert.Contains(body.Claims, claim =>
                claim.Text.Contains("monthly fee")
                && claim.CitationChunkIds.Count > 0);
            var claim = body.Claims.First(claim => claim.Text.Contains("monthly fee"));
            Assert.Contains("monthly fee", claim.Text);
            Assert.NotEmpty(claim.CitationChunkIds);

            Assert.NotEmpty(body.Citations);

            var firstCitation = body.Citations[0];

            Assert.Contains(firstCitation.ChunkId, claim.CitationChunkIds);
            Assert.False(string.IsNullOrWhiteSpace(firstCitation.DocumentId));
            Assert.False(string.IsNullOrWhiteSpace(firstCitation.ChunkId));
            Assert.Equal(filePath, firstCitation.SourceUri);
            Assert.True(firstCitation.RrfScore > 0);
            Assert.NotNull(firstCitation.RerankScore);
            Assert.True(firstCitation.RerankScore > 0);
            Assert.Null(firstCitation.VectorScore);
            Assert.NotNull(firstCitation.KeywordScore);
            Assert.Equal("keyword", firstCitation.MatchedBy);
            Assert.Null(firstCitation.PageNumber);
            Assert.Equal("Paragraph", firstCitation.Kind);

            Assert.Equal(5, body.RetrievalDiagnostics.RequestedTopK);
            Assert.True(body.RetrievalDiagnostics.CandidateCount > 0);
            Assert.True(body.RetrievalDiagnostics.ReturnedContextCount > 0);
            Assert.True(body.RetrievalDiagnostics.RerankingApplied);
            Assert.Equal(RetrievalStrategyNames.Hybrid, body.RetrievalDiagnostics.RetrievalMode);
            Assert.Equal(RetrievalStrategyNames.ApplicationRrf, body.RetrievalDiagnostics.FusionMode);
            Assert.Equal(RetrievalStrategyNames.LlmReranker, body.RetrievalDiagnostics.RerankerMode);
            Assert.Equal(60, body.RetrievalDiagnostics.RrfConstant);

            Assert.Equal("Ollama", body.ModelInfo.ChatProvider);
            Assert.False(string.IsNullOrWhiteSpace(body.ModelInfo.ChatModel));
            Assert.Equal("Ollama", body.ModelInfo.EmbeddingProvider);
            Assert.False(string.IsNullOrWhiteSpace(body.ModelInfo.EmbeddingModel));
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Ask_returns_layout_metadata_in_citations_for_scanned_documents()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("projectrag-api-scanned-ask-test-");

        try
        {
            var filePath = Path.Combine(tempDirectory.FullName, "invoice.pdf");

            await File.WriteAllBytesAsync(filePath, [1, 2, 3, 4]);

            var ingestResponse = await _client.PostAsJsonAsync(
                "/api/v1/ingestions",
                new StartIngestionRequest(filePath));

            var ingestion = await IngestionApiTestHelper.ReadQueuedIngestionAsync(ingestResponse);
            await IngestionApiTestHelper.ProcessQueuedIngestionAsync(_factory, ingestion);

            var askResponse = await _client.PostAsJsonAsync(
                "/api/v1/ask",
                new AskRequest("What is the amount due?", 5));

            Assert.Equal(HttpStatusCode.OK, askResponse.StatusCode);

            var body = await askResponse.Content.ReadFromJsonAsync<AskResponse>();

            Assert.NotNull(body);
            Assert.Equal("answered", body.AnswerStatus);
            Assert.NotEmpty(body.Claims);

            Assert.Equal(5, body.RetrievalDiagnostics.RequestedTopK);
            Assert.True(body.RetrievalDiagnostics.CandidateCount > 0);
            Assert.True(body.RetrievalDiagnostics.ReturnedContextCount > 0);
            Assert.True(body.RetrievalDiagnostics.RerankingApplied);
            Assert.Equal(RetrievalStrategyNames.Hybrid, body.RetrievalDiagnostics.RetrievalMode);
            Assert.Equal(RetrievalStrategyNames.ApplicationRrf, body.RetrievalDiagnostics.FusionMode);
            Assert.Equal(RetrievalStrategyNames.LlmReranker, body.RetrievalDiagnostics.RerankerMode);
            Assert.Equal(60, body.RetrievalDiagnostics.RrfConstant);

            Assert.Equal("Ollama", body.ModelInfo.ChatProvider);
            Assert.False(string.IsNullOrWhiteSpace(body.ModelInfo.ChatModel));
            Assert.Equal("Ollama", body.ModelInfo.EmbeddingProvider);
            Assert.False(string.IsNullOrWhiteSpace(body.ModelInfo.EmbeddingModel));

            Assert.Contains(body.Citations, citation =>
                citation.PageNumber == 1
                && citation.Kind == "Paragraph"
                && citation.SectionTitle == "Invoice 1001");

            var citation = body.Citations.First(citation =>
                citation.PageNumber == 1
                && citation.Kind == "Paragraph"
                && citation.SectionTitle == "Invoice 1001");

            Assert.NotNull(citation.RerankScore);
            Assert.True(citation.RerankScore > 0);
            Assert.Contains(body.Claims, claim =>
                claim.CitationChunkIds.Contains(citation.ChunkId));
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Ask_returns_native_rrf_diagnostics_when_configured()
    {
        using var factory = new RagApiFactory(
            new Dictionary<string, string?>
            {
                ["Retrieval:FusionMode"] = RetrievalStrategyNames.ElasticNativeRrf,
                ["Retrieval:EnableReranking"] = "false",
                ["Retrieval:RerankerMode"] = RetrievalStrategyNames.NoReranker
            },
            _ => new StaticRetrievalSearchService());

        using var client = factory.CreateClient();

        var askResponse = await client.PostAsJsonAsync(
            "/api/v1/ask",
            new AskRequest("What are the late payment fees?", TopK: 5));

        Assert.Equal(HttpStatusCode.OK, askResponse.StatusCode);

        var body = await askResponse.Content.ReadFromJsonAsync<AskResponse>();

        Assert.NotNull(body);
        Assert.Equal("answered", body.AnswerStatus);
        Assert.Equal(RetrievalStrategyNames.Hybrid, body.RetrievalDiagnostics.RetrievalMode);
        Assert.Equal(RetrievalStrategyNames.ElasticNativeRrf, body.RetrievalDiagnostics.FusionMode);
        Assert.Equal(RetrievalStrategyNames.NoReranker, body.RetrievalDiagnostics.RerankerMode);
        Assert.False(body.RetrievalDiagnostics.RerankingApplied);

        var citation = Assert.Single(body.Citations);

        Assert.Equal(RetrievalStrategyNames.ElasticNativeRrf, citation.MatchedBy);
        Assert.Null(citation.VectorScore);
        Assert.Null(citation.KeywordScore);
    }

    private sealed class StaticRetrievalSearchService : IRetrievalSearchService
    {
        public Task<IReadOnlyList<SearchHit>> SearchAsync(
            RetrievalQuery query,
            int topK,
            SearchFilters? filters,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<SearchHit> hits =
            [
                new SearchHit(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "source.md",
                    "Late balances may receive a monthly fee after a grace period.",
                    RrfScore: 1.23,
                    PageNumber: null,
                    ChunkKind.Paragraph,
                    SectionTitle: "Late Payment Policy",
                    VectorScore: null,
                    KeywordScore: null,
                    MatchedBy: RetrievalStrategyNames.ElasticNativeRrf)
            ];

            return Task.FromResult(hits);
        }
    }
}
