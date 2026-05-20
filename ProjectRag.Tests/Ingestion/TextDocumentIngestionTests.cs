using Microsoft.EntityFrameworkCore;
using ProjectRag.Domain.Entities;
using Microsoft.Extensions.Options;
using ProjectRag.Domain.Enums;
using ProjectRag.Infrastructure;
using ProjectRag.Infrastructure.Ingestion;
using ProjectRag.Infrastructure.Options;
using ProjectRag.Tests.Support;

namespace ProjectRag.Tests.Ingestion;

public sealed class TextDocumentIngestionTests
{
    [Fact]
    public void Chunk_splits_text_into_ordered_chunks()
    {
        var chunker = CreateChunker();

        var text = """
            # Late Payment Policy

            Invoices are due 30 calendar days after the invoice date.

            Late balances may receive a monthly fee after a grace period.
            """;

        var chunks = chunker.Chunk(text);

        Assert.NotEmpty(chunks);
        Assert.Equal(0, chunks[0].ChunkIndex);
        Assert.Contains("Invoices are due", chunks[0].Text);
        Assert.Equal("Late Payment Policy", chunks[0].SectionTitle);
    }

    [Fact]
    public void Chunk_uses_configured_max_chunk_size()
    {
        var chunker = CreateChunker(maxChunkSize: 10);

        var chunks = chunker.Chunk("one two three\n\nfour five six");

        Assert.Equal(2, chunks.Count);
        Assert.Equal(0, chunks[0].ChunkIndex);
        Assert.Equal(1, chunks[1].ChunkIndex);
    }

    [Fact]
    public void Chunk_keeps_current_paragraph_baseline()
    {
        var chunker = CreateChunker(maxChunkSize: 1200);

        var text = """
            # Late Payment Policy

            Invoices are due 30 calendar days after the invoice date.

            Late balances may receive a monthly fee after a grace period.
            """;

        var chunks = chunker.Chunk(text);

        var chunk = Assert.Single(chunks);

        Assert.Equal(0, chunk.ChunkIndex);
        Assert.Equal("Late Payment Policy", chunk.SectionTitle);
        Assert.Contains("Invoices are due", chunk.Text);
        Assert.Contains("monthly fee", chunk.Text);
    }

    [Fact]
    public async Task IngestPathAsync_creates_document_and_chunks_for_markdown_file()
    {
        using var database = new SqliteTestDatabase();
        await using var db = database.CreateContext();

        var tempDirectory = Directory.CreateTempSubdirectory("projectrag-ingestions-test-");

        try
        {
            var filePathOrDirectory = Path.Combine(tempDirectory.FullName, "late-payment-policy.md");

            await File.WriteAllTextAsync(filePathOrDirectory, """
                # Late Payment Policy
            
                Invoices are due 30 calendar days after the invoice date.

                Late balances may receive a monthly fee after a grace period.
                """);

            var searchIndexService = new FakeSearchIndexService();
            var service = new FileSystemDocumentIngestionService(
                db,
                CreateChunker(),
                new FakeDocumentExtractor(),
                searchIndexService,
                CreateChunkingOptions());

            var run = await CreateIngestionRunAsync(db, filePathOrDirectory);

            await service.IngestPathAsync(run.Id, run.KnowledgeBaseId, run.DataSourceId, filePathOrDirectory, CancellationToken.None);

            Assert.NotEmpty(searchIndexService.UpsertedChunks);
            Assert.Contains(searchIndexService.UpsertedChunks, x => x.Text.Contains("Invoices are due"));

            var document = await db.Documents
                .Include(x => x.Chunks)
                .SingleAsync();

            Assert.Equal(Path.GetFullPath(filePathOrDirectory), document.SourceUri);
            Assert.Equal("late-payment-policy", document.Title);
            Assert.Equal("md", document.SourceType);
            Assert.False(string.IsNullOrWhiteSpace(document.ContentHash));
            Assert.NotEmpty(document.Chunks);
            Assert.Contains(document.Chunks, x => x.Text.Contains("Invoices are due"));

        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task IngestPathAsync_creates_layout_aware_chunks_for_scanned_document()
    {
        using var database = new SqliteTestDatabase();
        await using var db = database.CreateContext();

        var tempDirectory = Directory.CreateTempSubdirectory("projectrag-scanned-ingestions-test-");

        try
        {
            var filePathOrDirectory = Path.Combine(tempDirectory.FullName, "invoice.pdf");

            await File.WriteAllBytesAsync(filePathOrDirectory, [1, 2, 3, 4]);

            var searchIndexService = new FakeSearchIndexService();
            var service = new FileSystemDocumentIngestionService(
                db,
                CreateChunker(),
                new FakeDocumentExtractor(),
                searchIndexService,
                CreateChunkingOptions());

            var run = await CreateIngestionRunAsync(db, filePathOrDirectory);

            await service.IngestPathAsync(run.Id, run.KnowledgeBaseId, run.DataSourceId, filePathOrDirectory, CancellationToken.None);

            Assert.Contains(searchIndexService.UpsertedChunks, x => x.Text.Contains("Total amount due"));

            var document = await db.Documents
                .Include(x => x.Chunks)
                .SingleAsync();

            Assert.Equal(Path.GetFullPath(filePathOrDirectory), document.SourceUri);
            Assert.Equal("invoice", document.Title);
            Assert.Equal("pdf", document.SourceType);

            var heading = Assert.Single(document.Chunks, x => x.Kind == ChunkKind.Heading);

            Assert.Equal(1, heading.PageNumber);
            Assert.Equal("Invoice 1001", heading.SectionTitle);
            Assert.Equal("title", heading.LayoutRole);
            Assert.False(string.IsNullOrWhiteSpace(heading.BoundingRegionsJson));

            var paragraph = Assert.Single(document.Chunks, x => x.Kind == ChunkKind.Paragraph);

            Assert.Equal(1, paragraph.PageNumber);
            Assert.Equal("Invoice 1001", paragraph.SectionTitle);
            Assert.Contains("Total amount due", paragraph.Text);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task IngestPathAsync_skips_unchanged_document_when_vectors_exist()
    {
        using var database = new SqliteTestDatabase();
        await using var db = database.CreateContext();

        var tempDirectory = Directory.CreateTempSubdirectory("projectrag-idempotency-test-");

        try
        {
            var filePathOrDirectory = Path.Combine(tempDirectory.FullName, "late-payment-policy.md");

            await File.WriteAllTextAsync(filePathOrDirectory, """
                # Late Payment Policy

                Invoices are due 30 calendar days after the invoice date.
                """);

            var searchIndexService = new FakeSearchIndexService();

            var service = new FileSystemDocumentIngestionService(
                db,
                CreateChunker(),
                new FakeDocumentExtractor(),
                searchIndexService,
                CreateChunkingOptions());

            var run = await CreateIngestionRunAsync(db, filePathOrDirectory);

            await service.IngestPathAsync(run.Id, run.KnowledgeBaseId, run.DataSourceId, filePathOrDirectory, CancellationToken.None);
            await service.IngestPathAsync(run.Id, run.KnowledgeBaseId, run.DataSourceId, filePathOrDirectory, CancellationToken.None);

            var documents = await db.Documents
                .Include(x => x.Chunks)
                .ToListAsync();

            var document = Assert.Single(documents);

            Assert.Single(document.Chunks);
            Assert.Single(searchIndexService.UpsertedChunks);
            Assert.Empty(searchIndexService.DeletedDocumentIds);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact(Skip = "Reingestion replacement behavior needs a separate EF tracking design pass.")]
    public async Task IngestPathAsync_replaces_chunks_and_vectors_when_file_changes()
    {
        using var database = new SqliteTestDatabase();
        await using var db = database.CreateContext();

        var tempDirectory = Directory.CreateTempSubdirectory("projectrag-reingestion-test-");

        try
        {
            var filePathOrDirectory = Path.Combine(tempDirectory.FullName, "late-payment-policy.md");

            await File.WriteAllTextAsync(filePathOrDirectory, """
                # Late Payment Policy

                Invoices are due 30 calendar days after the invoice date.
                """);

            var searchIndexService = new FakeSearchIndexService();

            var service = new FileSystemDocumentIngestionService(
                db,
                CreateChunker(),
                new FakeDocumentExtractor(),
                searchIndexService,
                CreateChunkingOptions());

            var run = await CreateIngestionRunAsync(db, filePathOrDirectory);

            await service.IngestPathAsync(run.Id, run.KnowledgeBaseId, run.DataSourceId, filePathOrDirectory, CancellationToken.None);

            await File.WriteAllTextAsync(filePathOrDirectory, """
                # Late Payment Policy

                Late balances may receive a monthly fee after a grace period.
                """);

            await service.IngestPathAsync(run.Id, run.KnowledgeBaseId, run.DataSourceId, filePathOrDirectory, CancellationToken.None);

            var document = await db.Documents
                .Include(x => x.Chunks)
                .SingleAsync();

            Assert.Single(document.Chunks);
            Assert.Contains(document.Chunks, x => x.Text.Contains("monthly fee"));
            Assert.Equal(2, searchIndexService.UpsertedChunks.Count);
            Assert.Single(searchIndexService.DeletedDocumentIds);
            Assert.Equal(document.Id, searchIndexService.DeletedDocumentIds[0]);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task IngestPathAsync_links_document_to_default_knowledge_base()
    {
        using var database = new SqliteTestDatabase();
        await using var db = database.CreateContext();

        var tempDirectory = Directory.CreateTempSubdirectory("projectrag-kb-ingestion-test-");

        try
        {
            var filePathOrDirectory = Path.Combine(tempDirectory.FullName, "late-payment-policy.md");

            await File.WriteAllTextAsync(filePathOrDirectory, """
              # Late Payment Policy

              Invoices are due 30 calendar days after the invoice date.
              """);

            var service = new FileSystemDocumentIngestionService(
                db,
                CreateChunker(),
                new FakeDocumentExtractor(),
                new FakeSearchIndexService(),
                CreateChunkingOptions());

            var run = await CreateIngestionRunAsync(db, filePathOrDirectory);

            await service.IngestPathAsync(run.Id, run.KnowledgeBaseId, run.DataSourceId, filePathOrDirectory, CancellationToken.None);

            var document = await db.Documents.SingleAsync();
            var knowledgeBase = await db.KnowledgeBases.SingleAsync();

            Assert.Equal("Default", knowledgeBase.Name);
            Assert.Equal(knowledgeBase.Id, document.KnowledgeBaseId);
            Assert.Equal(run.DataSourceId, document.DataSourceId);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task IngestPathAsync_creates_completed_item_for_each_ingested_file()
    {
        using var database = new SqliteTestDatabase();
        await using var db = database.CreateContext();

        var tempDirectory = Directory.CreateTempSubdirectory("projectrag-ingestion-items-test-");

        try
        {
            var firstPath = Path.Combine(tempDirectory.FullName, "one.md");
            var secondPath = Path.Combine(tempDirectory.FullName, "two.md");

            await File.WriteAllTextAsync(firstPath, "# One\n\nFirst document.");
            await File.WriteAllTextAsync(secondPath, "# Two\n\nSecond document.");

            var run = await CreateIngestionRunAsync(db, tempDirectory.FullName);

            var service = new FileSystemDocumentIngestionService(
                db,
                CreateChunker(),
                new FakeDocumentExtractor(),
                new FakeSearchIndexService(),
                CreateChunkingOptions());

            await service.IngestPathAsync(run.Id, run.KnowledgeBaseId, run.DataSourceId, tempDirectory.FullName, CancellationToken.None);

            var items = await db.IngestionItems
                .OrderBy(x => x.SourceUri)
                .ToListAsync();

            Assert.Equal(2, items.Count);
            Assert.All(items, item => Assert.Equal(run.Id, item.IngestionRunId));
            Assert.All(items, item => Assert.Equal(IngestionItemStatus.Completed, item.Status));
            Assert.All(items, item => Assert.NotNull(item.DocumentId));
            Assert.All(items, item => Assert.NotNull(item.StartedAt));
            Assert.All(items, item => Assert.NotNull(item.CompletedAt));
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task IngestPathAsync_indexes_chunking_metadata()
    {
        using var database = new SqliteTestDatabase();
        await using var db = database.CreateContext();

        var tempDirectory = Directory.CreateTempSubdirectory("projectrag-chunking-metadata-test-");

        try
        {
            var filePathOrDirectory = Path.Combine(tempDirectory.FullName, "late-payment-policy.md");

            await File.WriteAllTextAsync(filePathOrDirectory, """
                # Late Payment Policy

                Late balances may receive a monthly fee after a grace period.
                """);

            var searchIndexService = new FakeSearchIndexService();
            var service = new FileSystemDocumentIngestionService(
                db,
                CreateChunker(maxChunkSize: 40),
                new FakeDocumentExtractor(),
                searchIndexService,
                CreateChunkingOptions(maxChunkSize: 40, strategy: "Paragraph"));

            var run = await CreateIngestionRunAsync(db, filePathOrDirectory);

            await service.IngestPathAsync(run.Id, run.KnowledgeBaseId, run.DataSourceId, filePathOrDirectory, CancellationToken.None);

            Assert.NotEmpty(searchIndexService.UpsertedChunks);
            Assert.All(searchIndexService.UpsertedChunks, chunk =>
            {
                Assert.Equal(ChunkingStrategyNames.Paragraph, chunk.ChunkingStrategy);
                Assert.Equal(40, chunk.ChunkingMaxChunkSize);
            });
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    private static async Task<IngestionRun> CreateIngestionRunAsync(RagDbContext db, string sourcePath)
    {
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
            Name = Path.GetFileName(sourcePath),
            SourceType = Directory.Exists(sourcePath) ? "localFolder" : "localFile",
            SourceUri = Path.GetFullPath(sourcePath),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var run = new IngestionRun
        {
            Id = Guid.NewGuid(),
            KnowledgeBaseId = knowledgeBase.Id,
            DataSourceId = dataSource.Id,
            SourcePath = sourcePath,
            Status = IngestionRunStatus.Running,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow
        };

        db.KnowledgeBases.Add(knowledgeBase);
        db.DataSources.Add(dataSource);
        db.IngestionRuns.Add(run);
        await db.SaveChangesAsync();

        return run;
    }

    private static SimpleTextChunker CreateChunker(int maxChunkSize = 1200)
    {
        return new SimpleTextChunker(Options.Create(new ChunkingOptions
        {
            MaxChunkSize = maxChunkSize
        }));
    }

    private static IOptions<ChunkingOptions> CreateChunkingOptions(
        int maxChunkSize = 1200,
        string? strategy = null)
    {
        return Options.Create(new ChunkingOptions
        {
            Strategy = strategy ?? ChunkingStrategyNames.Paragraph,
            MaxChunkSize = maxChunkSize
        });
    }
}
