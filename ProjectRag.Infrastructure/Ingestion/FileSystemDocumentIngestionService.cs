using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectRag.Application.Abstractions;
using ProjectRag.Application.Models;
using ProjectRag.Application.Telemetry;
using ProjectRag.Domain.Entities;
using ProjectRag.Domain.Enums;
using ProjectRag.Infrastructure.Options;
using System.Security.Cryptography;
using System.Text.Json;

namespace ProjectRag.Infrastructure.Ingestion;

internal sealed class FileSystemDocumentIngestionService : ITextDocumentIngestionService
{
    private static readonly string[] TextExtensions = [".md", ".txt"];
    private static readonly string[] ScannedExtensions = [".pdf", ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff"];
    private static readonly string[] SupportedExtensions = [.. TextExtensions, .. ScannedExtensions];

    private readonly RagDbContext _db;
    private readonly ITextChunker _chunker;
    private readonly IDocumentExtractor _documentExtractor;
    private readonly ISearchIndexService _searchIndexService;
    private readonly ChunkingOptions _chunkingOptions;

    private string ChunkingStrategy => ChunkingStrategyNames.Normalize(_chunkingOptions.Strategy);

    public FileSystemDocumentIngestionService(
        RagDbContext db,
        ITextChunker chunker,
        IDocumentExtractor documentExtractor,
        ISearchIndexService searchIndexService,
        IOptions<ChunkingOptions> chunkingOptions)
    {
        _db = db;
        _chunker = chunker;
        _documentExtractor = documentExtractor;
        _searchIndexService = searchIndexService;
        _chunkingOptions = chunkingOptions.Value;
    }

    public async Task IngestPathAsync(
        Guid ingestionRunId,
        Guid knowledgeBaseId,
        Guid? dataSourceId,
        string sourcePath,
        CancellationToken cancellationToken)
    {
        using var activity = ProjectRagTelemetry.ActivitySource.StartActivity("rag.ingestion");
        activity?.SetTag("rag.source_path.exists_file", File.Exists(sourcePath));
        activity?.SetTag("rag.source_path.exists_directory", Directory.Exists(sourcePath));

        var files = ResolveFiles(sourcePath);
        activity?.SetTag("rag.files.count", files.Count);

        foreach (var file in files)
        {
            await IngestFileAsync(ingestionRunId, knowledgeBaseId, dataSourceId, file, cancellationToken);
        }
    }

    private async Task IngestFileAsync(
        Guid ingestionRunId,
        Guid knowledgeBaseId,
        Guid? dataSourceId,
        string filePath,
        CancellationToken cancellationToken)
    {
        using var activity = ProjectRagTelemetry.ActivitySource.StartActivity("rag.ingestion.file");
        activity?.SetTag("rag.source_type", Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant());

        var item = new IngestionItem
        {
            Id = Guid.NewGuid(),
            IngestionRunId = ingestionRunId,
            SourceUri = filePath,
            Status = IngestionItemStatus.Running,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
        };

        try
        {
            _db.IngestionItems.Add(item);
            await _db.SaveChangesAsync(cancellationToken);

            var contentHash = await ComputeFileHashAsync(filePath, cancellationToken);
            var extension = Path.GetExtension(filePath);

            var isTextDocument = TextExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);

            var existing = await _db.Documents.SingleOrDefaultAsync(x => x.SourceUri == filePath, cancellationToken); // get existing document

            // skip document - content hasn't changed
            if (existing is not null && existing.ContentHash == contentHash)
            {
                var hasIndexedChunks = await _searchIndexService.DocumentHasIndexedChunksAsync(existing.Id, cancellationToken);

                if (hasIndexedChunks)
                {
                    activity?.SetTag("rag.ingestion.skipped", true);
                    activity?.SetTag("rag.ingestion.skip_reason", "unchanged");
                    item.DocumentId = existing.Id;
                    item.Status = IngestionItemStatus.Skipped;
                    item.CompletedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync(cancellationToken);
                    return;
                }

                await _db.Entry(existing).Collection(x => x.Chunks).LoadAsync(cancellationToken); // load changes before indexing document chunks

                await IndexDocumentChunksAsync(existing, cancellationToken); // reindex existing document that is missing search index records
                activity?.SetTag("rag.ingestion.reindexed_existing", true);

                // skip ingestion item
                item.DocumentId = existing.Id;
                item.Status = IngestionItemStatus.Skipped;
                item.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);

                return;
            }

            activity?.SetTag("rag.ingestion.skipped", false);
            activity?.SetTag("rag.ingestion.is_reingestion", existing is not null);

            // ingest new document or reingest changed document
            Document document;
            if (existing is not null)
            {
                await _searchIndexService.DeleteDocumentAsync(existing.Id, cancellationToken);

                await _db.DocumentChunks.Where(x => x.DocumentId == existing.Id).ExecuteDeleteAsync(cancellationToken);

                existing.Title = Path.GetFileNameWithoutExtension(filePath);
                existing.ContentHash = contentHash;
                existing.SourceType = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
                existing.KnowledgeBaseId = knowledgeBaseId;
                existing.DataSourceId = dataSourceId;
                existing.UpdatedAt = DateTime.UtcNow;

                document = existing;
            }
            else
            {
                document = new Document
                {
                    Id = Guid.NewGuid(),
                    SourceUri = filePath,
                    Title = Path.GetFileNameWithoutExtension(filePath),
                    ContentHash = contentHash,
                    SourceType = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant(),
                    KnowledgeBaseId = knowledgeBaseId,
                    DataSourceId = dataSourceId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };

                _db.Documents.Add(document);
            }

            if (isTextDocument)
            {
                var text = await File.ReadAllTextAsync(filePath, cancellationToken);
                AddTextChunks(document, text);
            }
            else
            {
                await AddExtractedChunksAsync(document, filePath, cancellationToken);
            }

            await _db.SaveChangesAsync(cancellationToken);
            activity?.SetTag("rag.chunking.strategy", ChunkingStrategy);
            activity?.SetTag("rag.chunking.max_chunk_size", _chunkingOptions.MaxChunkSize);
            activity?.SetTag("rag.chunks.count", document.Chunks.Count);

            item.DocumentId = document.Id;

            await IndexDocumentChunksAsync(document, cancellationToken);

            item.Status = IngestionItemStatus.Completed;
            item.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            item.Status = IngestionItemStatus.Failed;
            item.ErrorMessage = ex.Message;
            item.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            throw;
        }
    }

    private async Task IndexDocumentChunksAsync(Document document, CancellationToken cancellationToken)
    {
        var chunks = document.Chunks
            .Select(chunk => new SearchIndexChunk(
                document.Id,
                chunk.Id,
                document.SourceUri,
                document.SourceType,
                document.Title,
                chunk.Text,
                chunk.PageNumber,
                chunk.SectionTitle,
                chunk.Kind,
                chunk.CreatedAt,
                ChunkingStrategy,
                _chunkingOptions.MaxChunkSize))
            .ToList();

        await _searchIndexService.UpsertChunksAsync(chunks, cancellationToken);
    }

    private async Task AddExtractedChunksAsync(Document document, string filePath, CancellationToken cancellationToken)
    {
        var extracted = await _documentExtractor.ExtractAsync(filePath, cancellationToken);
        var currentSectionTitle = default(string);

        foreach (var block in extracted.Blocks)
        {
            if (!string.IsNullOrWhiteSpace(block.SectionTitle))
            {
                currentSectionTitle = block.SectionTitle;
            }

            document.Chunks.Add(new DocumentChunk
            {
                Id = Guid.NewGuid(),
                ChunkIndex = block.BlockIndex,
                Text = block.Text,
                PageNumber = block.PageNumber,
                SectionTitle = block.SectionTitle ?? currentSectionTitle,
                LayoutRole = block.LayoutRole,
                BoundingRegionsJson = JsonSerializer.Serialize(block.BoundingRegions),
                Kind = block.Kind,
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    private void AddTextChunks(Document document, string text)
    {
        foreach (var chunk in _chunker.Chunk(text))
        {
            document.Chunks.Add(new DocumentChunk
            {
                Id = Guid.NewGuid(),
                ChunkIndex = chunk.ChunkIndex,
                Text = chunk.Text,
                SectionTitle = chunk.SectionTitle,
                Kind = ChunkKind.Paragraph,
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    private static IReadOnlyList<string> ResolveFiles(string sourcePath)
    {
        if (File.Exists(sourcePath))
        {
            return IsSupportedFile(sourcePath) ? [Path.GetFullPath(sourcePath)] : [];
        }

        if (Directory.Exists(sourcePath))
        {
            return Directory
                .EnumerateFiles(sourcePath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(IsSupportedFile)
                .Select(Path.GetFullPath)
                .OrderBy(x => x)
                .ToList();
        }

        throw new FileNotFoundException($"Source path '{sourcePath}' was not found.", sourcePath);
    }

    private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);

        return Convert.ToHexString(hash);
    }

    private static bool IsSupportedFile(string path)
    {
        var extension = Path.GetExtension(path);
        return SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}
