using ProjectRag.Contracts;
using ProjectRag.Tests.Support;
using System.Net;
using System.Net.Http.Json;

namespace ProjectRag.Tests.Api;

public sealed class IngestionEndpointsTests : IClassFixture<RagApiFactory>
{
    private readonly HttpClient _client;

    public IngestionEndpointsTests(RagApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostIngestion_queues_ingestion_run()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("projectrag-api-ingestion-test-");

        try
        {
            var filePath = Path.Combine(tempDirectory.FullName, "late-payment-policy.md");

            await File.WriteAllTextAsync(filePath, """
                # Late Payment Policy

                Invoices are due 30 calendar days after the invoice date.
                """);

            var request = new StartIngestionRequest(filePath);

            var response = await _client.PostAsJsonAsync("/api/v1/ingestions", request);

            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<IngestionRunResponse>();

            Assert.NotNull(body);

            Assert.NotEqual(Guid.Empty, body.IngestionId);
            Assert.NotEqual(Guid.Empty, body.KnowledgeBaseId);
            Assert.NotNull(body.DataSourceId);
            Assert.NotEqual(Guid.Empty, body.DataSourceId.Value);
            Assert.Equal(filePath, body.SourcePath);

            Assert.Equal("Pending", body.Status);
            Assert.Null(body.StartedAt);
            Assert.Null(body.CompletedAt);

            Assert.NotNull(body.Summary);
            Assert.Equal(0, body.Summary.TotalItems);
            Assert.Equal(0, body.Summary.CompletedItems);
            Assert.Equal(0, body.Summary.FailedItems);
            Assert.Equal(0, body.Summary.SkippedItems);
            Assert.Empty(body.Items);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task GetIngestion_returns_created_run()
    {
        var tempDirectory = Directory.CreateTempSubdirectory("projectrag-api-ingestion-test-");

        try
        {
            var filePath = Path.Combine(tempDirectory.FullName, "invoice-disputes.md");

            await File.WriteAllTextAsync(filePath, """
                # Invoice Disputes

                Customers must submit invoice disputes within 15 calendar days.
                """);

            var createResponse = await _client.PostAsJsonAsync(
                "/api/v1/ingestions",
                new StartIngestionRequest(filePath));

            var created = await createResponse.Content.ReadFromJsonAsync<IngestionRunResponse>();

            Assert.NotNull(created);

            var getResponse = await _client.GetAsync($"/api/v1/ingestions/{created.IngestionId}");

            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            var fetched = await getResponse.Content.ReadFromJsonAsync<IngestionRunResponse>();

            Assert.NotNull(fetched);

            Assert.Equal(created.IngestionId, fetched.IngestionId);
            Assert.Equal(created.KnowledgeBaseId, fetched.KnowledgeBaseId);
            Assert.Equal(created.DataSourceId, fetched.DataSourceId);
            Assert.Equal(filePath, fetched.SourcePath);

            Assert.Equal("Pending", fetched.Status);
            Assert.Empty(fetched.Items);
            Assert.NotNull(fetched.Summary);
            Assert.Equal(0, fetched.Summary.TotalItems);
            Assert.Equal(fetched.Summary.TotalItems, fetched.Items.Count);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }
}
