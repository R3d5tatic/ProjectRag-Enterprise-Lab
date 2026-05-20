using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectRag.Application.Abstractions;
using ProjectRag.Application.Models;
using ProjectRag.Domain.Entities;
using ProjectRag.Domain.Enums;
using ProjectRag.Infrastructure;
using ProjectRag.Infrastructure.Ingestion;
using ProjectRag.Infrastructure.Options;
using ProjectRag.Tests.Support;

namespace ProjectRag.Tests.Ingestion;

public sealed class IngestionRunProcessorTests
{
    [Fact]
    public async Task ProcessAsync_creates_run_data_source_items_and_documents()
    {
        using var database = new SqliteTestDatabase();
        await using var db = database.CreateContext();

        var tempDirectory = Directory.CreateTempSubdirectory("projectrag-run-processor-test-");

        try
        {
            var filePath = Path.Combine(tempDirectory.FullName, "policy.md");

            await File.WriteAllTextAsync(filePath, """
                # Policy

                This document should ingest successfully.
                """);

            var queue = CreateQueueService(db);
            var processor = CreateProcessor(db);

            var queued = await queue.QueueAsync(filePath, CancellationToken.None);
            var result = await processor.ProcessAsync(queued.IngestionId, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("Completed", result.Status);
            Assert.Equal(filePath, result.SourcePath);

            Assert.Equal(1, result.Summary.TotalItems);
            Assert.Equal(1, result.Summary.CompletedItems);
            Assert.Equal(0, result.Summary.FailedItems);

            var item = Assert.Single(result.Items);
            Assert.Equal("Completed", item.Status);
            Assert.Equal(filePath, item.SourceUri);
            Assert.NotNull(item.DocumentId);

            var knowledgeBase = await db.KnowledgeBases.SingleAsync();
            var dataSource = await db.DataSources.SingleAsync();
            var run = await db.IngestionRuns.SingleAsync();
            var document = await db.Documents.SingleAsync();

            Assert.Equal("Default", knowledgeBase.Name);
            Assert.Equal(knowledgeBase.Id, dataSource.KnowledgeBaseId);
            Assert.Equal(knowledgeBase.Id, run.KnowledgeBaseId);
            Assert.Equal(dataSource.Id, run.DataSourceId);
            Assert.Equal(knowledgeBase.Id, document.KnowledgeBaseId);
            Assert.Equal(dataSource.Id, document.DataSourceId);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ProcessAsync_returns_failed_run_when_file_ingestion_fails()
    {
        using var database = new SqliteTestDatabase();
        await using var db = database.CreateContext();

        var tempDirectory = Directory.CreateTempSubdirectory("projectrag-run-processor-failure-test-");

        try
        {
            var markdownPath = Path.Combine(tempDirectory.FullName, "a-valid.md");
            var scannedPath = Path.Combine(tempDirectory.FullName, "z-broken.pdf");

            await File.WriteAllTextAsync(markdownPath, "# Valid\n\nThis succeeds.");
            await File.WriteAllBytesAsync(scannedPath, [1, 2, 3, 4]);

            var queue = CreateQueueService(db);
            var processor = CreateProcessor(db, new ThrowingDocumentExtractor());

            var queued = await queue.QueueAsync(tempDirectory.FullName, CancellationToken.None);
            var result = await processor.ProcessAsync(queued.IngestionId, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("Failed", result.Status);
            Assert.Equal(2, result.Summary.TotalItems);
            Assert.Equal(1, result.Summary.CompletedItems);
            Assert.Equal(1, result.Summary.FailedItems);

            var completedItem = Assert.Single(result.Items, item =>
                item.SourceUri == markdownPath &&
                item.Status == "Completed");
            Assert.NotNull(completedItem.DocumentId);

            var failedItem = Assert.Single(result.Items, item =>
                item.SourceUri == scannedPath &&
                item.Status == "Failed");

            Assert.Null(failedItem.DocumentId);
            Assert.Contains("Scanned extraction failed for test", failedItem.ErrorMessage);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ProcessAsync_processes_queued_run()
    {
        using var database = new SqliteTestDatabase();
        await using var db = database.CreateContext();

        var tempDirectory = Directory.CreateTempSubdirectory("projectrag-process-test-");

        try
        {
            var filePath = Path.Combine(tempDirectory.FullName, "policy.md");
            await File.WriteAllTextAsync(filePath, "# Policy\n\nProcess this.");

            var queue = CreateQueueService(db);
            var processor = CreateProcessor(db);

            var queued = await queue.QueueAsync(filePath, CancellationToken.None);
            var processed = await processor.ProcessAsync(queued.IngestionId, CancellationToken.None);

            Assert.NotNull(processed);
            Assert.Equal("Completed", processed.Status);
            Assert.Equal(1, processed.Summary.CompletedItems);

            var item = Assert.Single(processed.Items);
            Assert.NotNull(item.DocumentId);

            Assert.Single(await db.Documents.ToListAsync());
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData(IngestionRunStatus.Running)]
    [InlineData(IngestionRunStatus.Completed)]
    [InlineData(IngestionRunStatus.Failed)]
    public async Task ProcessAsync_does_not_process_non_pending_run(IngestionRunStatus status)
    {
        using var database = new SqliteTestDatabase();
        await using var db = database.CreateContext();

        var tempDirectory = Directory.CreateTempSubdirectory("projectrag-process-non-pending-test-");

        try
        {
            var filePath = Path.Combine(tempDirectory.FullName, "policy.md");
            await File.WriteAllTextAsync(filePath, "# Policy\n\nShould not process.");

            var knowledgeBase = new KnowledgeBase
            {
                Id = Guid.NewGuid(),
                Name = "Default",
                Description = "Default local knowledge base",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var dataSource = new DataSource
            {
                Id = Guid.NewGuid(),
                KnowledgeBaseId = knowledgeBase.Id,
                Name = "policy.md",
                SourceType = "localFile",
                SourceUri = Path.GetFullPath(filePath),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var run = new IngestionRun
            {
                Id = Guid.NewGuid(),
                KnowledgeBaseId = knowledgeBase.Id,
                DataSourceId = dataSource.Id,
                SourcePath = filePath,
                Status = status,
                CreatedAt = DateTime.UtcNow,
                StartedAt = status == IngestionRunStatus.Running ? DateTime.UtcNow : null,
                CompletedAt = status is IngestionRunStatus.Completed or IngestionRunStatus.Failed
                    ? DateTime.UtcNow
                    : null
            };

            db.KnowledgeBases.Add(knowledgeBase);
            db.DataSources.Add(dataSource);
            db.IngestionRuns.Add(run);
            await db.SaveChangesAsync();

            var processor = CreateProcessor(db);

            var result = await processor.ProcessAsync(run.Id, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(status.ToString(), result.Status);
            Assert.Empty(result.Items);
            Assert.Empty(await db.Documents.ToListAsync());
            Assert.Empty(await db.IngestionItems.ToListAsync());
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    private static IngestionRunQueueService CreateQueueService(RagDbContext db)
    {
        return new IngestionRunQueueService(db);
    }

    private static IngestionRunProcessor CreateProcessor(
        RagDbContext db,
        IDocumentExtractor? documentExtractor = null)
    {
        var ingestionService = new FileSystemDocumentIngestionService(
            db,
            new SimpleTextChunker(Options.Create(new ChunkingOptions())),
            documentExtractor ?? new FakeDocumentExtractor(),
            new FakeSearchIndexService(),
            Options.Create(new ChunkingOptions()));

        return new IngestionRunProcessor(db, ingestionService);
    }

    private sealed class ThrowingDocumentExtractor : IDocumentExtractor
    {
        public Task<ExtractedDocument> ExtractAsync(string filePath, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Scanned extraction failed for test.");
        }
    }
}
