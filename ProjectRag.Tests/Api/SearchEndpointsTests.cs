using ProjectRag.Contracts;
using ProjectRag.Tests.Support;
using System.Net;
using System.Net.Http.Json;

namespace ProjectRag.Tests.Api;

public sealed class SearchEndpointsTests : IClassFixture<RagApiFactory>
{
    private readonly HttpClient _client;
    private readonly RagApiFactory _factory;

    public SearchEndpointsTests(RagApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Search_returns_ranked_hits_for_ingested_documents()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("projectrag-search-test-");

        try
        {
            var filePath = Path.Combine(tempDirectory.FullName, "late-payment-policy.md");
            SampleDocsTestHelper.CopySampleDocs(tempDirectory.FullName);

            var ingestionResponse = await _client.PostAsJsonAsync(
                "/api/v1/ingestions",
                new StartIngestionRequest(tempDirectory.FullName));

            var ingestion = await IngestionApiTestHelper.ReadQueuedIngestionAsync(ingestionResponse);
            await IngestionApiTestHelper.ProcessQueuedIngestionAsync(_factory, ingestion);

            var searchResponse = await _client.PostAsJsonAsync(
                "/api/v1/search",
                new SearchRequest("late payment fees", TopK: 20));

            Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

            var body = await searchResponse.Content.ReadFromJsonAsync<SearchResponse>();

            Assert.NotNull(body);

            Assert.Equal("late payment fees", body.Query);

            Assert.NotNull(body.QueryRewrite);
            Assert.Equal("late payment fees", body.QueryRewrite.OriginalQuery);
            Assert.Equal("late payment fees", body.QueryRewrite.SemanticQuery);
            Assert.Equal("late payment fees", body.QueryRewrite.KeywordQuery);
            Assert.Equal("test-fake", body.QueryRewrite.Status);

            Assert.NotEmpty(body.Results);

            var hit = Assert.Single(
                body.Results,
                result => result.SourceUri == filePath);

            Assert.False(string.IsNullOrWhiteSpace(hit.DocumentId));
            Assert.False(string.IsNullOrWhiteSpace(hit.ChunkId));
            Assert.Equal(filePath, hit.SourceUri);
            Assert.Contains("monthly fee", hit.TextPreview);

            Assert.True(hit.RrfScore > 0);
            Assert.NotNull(hit.RerankScore);
            Assert.True(hit.RerankScore > 0);
            Assert.Null(hit.VectorScore);
            Assert.NotNull(hit.KeywordScore);
            Assert.Equal("keyword", hit.MatchedBy);

            Assert.Equal("Paragraph", hit.Kind);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Search_returns_layout_metadata_for_scanned_dcouments()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("projectrag-api-scanned-search-test-");

        try
        {
            var filePath = Path.Combine(tempDirectory.FullName, "invoice.png");

            await File.WriteAllBytesAsync(filePath, [1, 2, 3, 4]);

            var ingestionResponse = await _client.PostAsJsonAsync(
                "/api/v1/ingestions",
                new StartIngestionRequest(filePath));

            var ingestion = await IngestionApiTestHelper.ReadQueuedIngestionAsync(ingestionResponse);
            await IngestionApiTestHelper.ProcessQueuedIngestionAsync(_factory, ingestion);

            var searchResponse = await _client.PostAsJsonAsync(
                "/api/v1/search",
                new SearchRequest("amount due", TopK: 5));

            Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

            var body = await searchResponse.Content.ReadFromJsonAsync<SearchResponse>();

            Assert.NotNull(body);
            Assert.NotEmpty(body.Results);

            Assert.Contains(body.Results, result =>
                result.PageNumber == 1
                && result.Kind == "Paragraph"
                && result.SectionTitle == "Invoice 1001");

            var hit = body.Results.First(result =>
                result.PageNumber == 1
                && result.Kind == "Paragraph"
                && result.SectionTitle == "Invoice 1001");

            Assert.NotNull(hit.RerankScore);
            Assert.True(hit.RerankScore > 0);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Search_applies_source_type_filter()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("projectrag-search-filter-test-");

        try
        {
            var markdownPath = Path.Combine(tempDirectory.FullName, "late-payment-policy.md");
            var textPath = Path.Combine(tempDirectory.FullName, "late-payment-policy.txt");

            await File.WriteAllTextAsync(markdownPath, """
                # Late Payment Policy

                Late balances may receive a monthly fee after a grace period.
                """);

            await File.WriteAllTextAsync(textPath, """
                Late Payment Notes

                This text file also mentions late payment fees.
                """);

            var ingestionResponse = await _client.PostAsJsonAsync(
                "/api/v1/ingestions",
                new StartIngestionRequest(tempDirectory.FullName));

            var ingestion = await IngestionApiTestHelper.ReadQueuedIngestionAsync(ingestionResponse);
            await IngestionApiTestHelper.ProcessQueuedIngestionAsync(_factory, ingestion);

            var searchResponse = await _client.PostAsJsonAsync(
                "/api/v1/search",
                new SearchRequest(
                    "late payment fees",
                    TopK: 10,
                    Filters: new SearchFiltersRequest(SourceType: "md")));

            Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

            var body = await searchResponse.Content.ReadFromJsonAsync<SearchResponse>();

            Assert.NotNull(body);
            Assert.NotEmpty(body.Results);

            Assert.All(body.Results, result => Assert.NotNull(result.RerankScore));
            Assert.All(body.Results, result => Assert.EndsWith(".md", result.SourceUri));
            Assert.DoesNotContain(body.Results, result => result.SourceUri == textPath);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }
}
