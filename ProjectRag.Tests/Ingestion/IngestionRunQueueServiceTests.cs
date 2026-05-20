using Microsoft.EntityFrameworkCore;
using ProjectRag.Infrastructure;
using ProjectRag.Infrastructure.Ingestion;
using ProjectRag.Tests.Support;

namespace ProjectRag.Tests.Ingestion;

public sealed class IngestionRunQueueServiceTests
{
    [Fact]
    public async Task QueueAsync_creates_pending_run_without_ingesting_documents()
    {
        using var database = new SqliteTestDatabase();
        await using var db = database.CreateContext();

        var tempDirectory = Directory.CreateTempSubdirectory("projectrag-queue-test-");

        try
        {
            var filePath = Path.Combine(tempDirectory.FullName, "policy.md");
            await File.WriteAllTextAsync(filePath, "# Policy\n\nQueued only.");

            var service = CreateQueueService(db);

            var result = await service.QueueAsync(filePath, CancellationToken.None);

            Assert.Equal("Pending", result.Status);
            Assert.Equal(0, result.Summary.TotalItems);
            Assert.Empty(result.Items);

            Assert.Single(await db.KnowledgeBases.ToListAsync());
            Assert.Single(await db.DataSources.ToListAsync());
            Assert.Single(await db.IngestionRuns.ToListAsync());
            Assert.Empty(await db.Documents.ToListAsync());
            Assert.Empty(await db.IngestionItems.ToListAsync());
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task QueueAsync_normalizes_source_path()
    {
        using var database = new SqliteTestDatabase();
        await using var db = database.CreateContext();

        var tempDirectory = Directory.CreateTempSubdirectory("projectrag-queue-normalize-test-");

        try
        {
            var filePath = Path.Combine(tempDirectory.FullName, "policy.md");
            await File.WriteAllTextAsync(filePath, "# Policy\n\nQueued from relative path.");

            var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), filePath);
            var expectedPath = Path.GetFullPath(relativePath);
            var service = CreateQueueService(db);

            var result = await service.QueueAsync(relativePath, CancellationToken.None);

            Assert.Equal(expectedPath, result.SourcePath);

            var run = await db.IngestionRuns.SingleAsync();
            var dataSource = await db.DataSources.SingleAsync();

            Assert.Equal(expectedPath, run.SourcePath);
            Assert.Equal(expectedPath, dataSource.SourceUri);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task GetAsync_returns_run()
    {
        using var database = new SqliteTestDatabase();
        await using var db = database.CreateContext();

        var tempDirectory = Directory.CreateTempSubdirectory("projectrag-run-service-get-test-");

        try
        {
            var filePath = Path.Combine(tempDirectory.FullName, "policy.md");
            await File.WriteAllTextAsync(filePath, "# Policy\n\nThis document should queue.");

            var service = CreateQueueService(db);

            var queued = await service.QueueAsync(filePath, CancellationToken.None);
            var fetched = await service.GetAsync(queued.IngestionId, CancellationToken.None);

            Assert.NotNull(fetched);
            Assert.Equal(queued.IngestionId, fetched.IngestionId);
            Assert.Equal("Pending", fetched.Status);
            Assert.Equal(0, fetched.Summary.TotalItems);
            Assert.Empty(fetched.Items);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task GetAsync_returns_null_for_missing_run()
    {
        using var database = new SqliteTestDatabase();
        await using var db = database.CreateContext();

        var service = CreateQueueService(db);

        var result = await service.GetAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    private static IngestionRunQueueService CreateQueueService(RagDbContext db)
    {
        return new IngestionRunQueueService(db);
    }
}
