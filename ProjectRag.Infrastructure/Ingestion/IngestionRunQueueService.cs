using Microsoft.EntityFrameworkCore;
using ProjectRag.Application.Abstractions;
using ProjectRag.Application.Models;
using ProjectRag.Domain.Entities;
using ProjectRag.Domain.Enums;

namespace ProjectRag.Infrastructure.Ingestion;

internal sealed class IngestionRunQueueService : IIngestionRunQueueService
{
    private readonly RagDbContext _db;

    public IngestionRunQueueService(RagDbContext db)
    {
        _db = db;
    }

    public async Task<IngestionRunResult> QueueAsync(string sourcePath, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(sourcePath);
        var knowledgeBase = await ResolveDefaultKnowledgeBaseAsync(cancellationToken);
        var dataSource = await ResolveDataSourceAsync(knowledgeBase.Id, fullPath, cancellationToken);

        var run = new IngestionRun
        {
            Id = Guid.NewGuid(),
            KnowledgeBaseId = knowledgeBase.Id,
            DataSourceId = dataSource.Id,
            SourcePath = fullPath,
            Status = IngestionRunStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.IngestionRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        return ToResult(run);
    }
    public async Task<IngestionRunResult?> GetAsync(Guid ingestionRunId, CancellationToken cancellationToken)
    {
        var run = await _db.IngestionRuns
            .AsNoTracking()
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == ingestionRunId, cancellationToken);

        return run is null ? null : ToResult(run);
    }

    private async Task<KnowledgeBase> ResolveDefaultKnowledgeBaseAsync(CancellationToken cancellationToken)
    {
        const string defaultName = "Default";

        var knowledgeBase = await _db.KnowledgeBases.SingleOrDefaultAsync(x => x.Name == defaultName, cancellationToken);

        if (knowledgeBase is not null)
        {
            return knowledgeBase;
        }

        knowledgeBase = new KnowledgeBase
        {
            Id = Guid.NewGuid(),
            Name = defaultName,
            Description = "Default local knowledge base",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.KnowledgeBases.Add(knowledgeBase);

        return knowledgeBase;
    }

    private async Task<DataSource> ResolveDataSourceAsync(Guid knowledgeBaseId, string sourcePath, CancellationToken cancellationToken)
    {
        var sourceType = Directory.Exists(sourcePath) ? "localFolder" : "localFile";
        var name = Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        if (string.IsNullOrWhiteSpace(name))
        {
            name = sourcePath;
        }

        var dataSource = await _db.DataSources
            .SingleOrDefaultAsync(x =>
                x.KnowledgeBaseId == knowledgeBaseId &&
                x.SourceUri == sourcePath,
                cancellationToken);

        if (dataSource is not null)
        {
            dataSource.UpdatedAt = DateTime.UtcNow;
            return dataSource;
        }

        dataSource = new DataSource
        {
            Id = Guid.NewGuid(),
            KnowledgeBaseId = knowledgeBaseId,
            Name = name,
            SourceType = sourceType,
            SourceUri = sourcePath,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.DataSources.Add(dataSource);

        return dataSource;
    }

    private static IngestionRunResult ToResult(IngestionRun run)
    {
        var items = run.Items
            .OrderBy(x => x.SourceUri)
            .Select(x => new IngestionItemResult(
                x.Id,
                x.DocumentId,
                x.SourceUri,
                x.Status.ToString(),
                x.ErrorMessage,
                x.CreatedAt,
                x.StartedAt,
                x.CompletedAt))
            .ToList();

        var summary = new IngestionRunSummary(
            TotalItems: items.Count,
            CompletedItems: items.Count(x => x.Status == "Completed"),
            FailedItems: items.Count(x => x.Status == "Failed"),
            SkippedItems: items.Count(x => x.Status == "Skipped"));

        return new IngestionRunResult(
          run.Id,
          run.KnowledgeBaseId,
          run.DataSourceId,
          run.SourcePath,
          run.Status.ToString(),
          run.ErrorMessage,
          run.CreatedAt,
          run.StartedAt,
          run.CompletedAt,
          summary,
          items);
    }
}
