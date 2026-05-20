using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProjectRag.Contracts;
using ProjectRag.Infrastructure;
using ProjectRag.Infrastructure.Ingestion;
using ProjectRag.Infrastructure.Options;
using System.Net;
using System.Net.Http.Json;

namespace ProjectRag.Tests.Support;

internal static class IngestionApiTestHelper
{
    public static async Task<IngestionRunResponse> ReadQueuedIngestionAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<IngestionRunResponse>();

        Assert.NotNull(body);
        Assert.Equal("Pending", body.Status);

        return body;
    }

    public static async Task ProcessQueuedIngestionAsync(
        RagApiFactory factory,
        IngestionRunResponse queuedIngestion)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RagDbContext>();
        var ingestionService = new FileSystemDocumentIngestionService(
            db,
            new SimpleTextChunker(Options.Create(new ChunkingOptions())),
            new FakeDocumentExtractor(),
            new FakeSearchIndexService(),
            Options.Create(new ChunkingOptions()));
        var ingestionRunProcessor = new IngestionRunProcessor(db, ingestionService);

        var processed = await ingestionRunProcessor.ProcessAsync(
            queuedIngestion.IngestionId,
            CancellationToken.None);

        Assert.NotNull(processed);
        Assert.Equal("Completed", processed.Status);
    }
}
