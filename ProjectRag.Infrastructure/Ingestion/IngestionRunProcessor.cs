using Microsoft.EntityFrameworkCore;
using ProjectRag.Application.Abstractions;
using ProjectRag.Application.Models;
using ProjectRag.Domain.Entities;
using ProjectRag.Domain.Enums;

namespace ProjectRag.Infrastructure.Ingestion;

internal sealed class IngestionRunProcessor : IIngestionRunProcessor
{
    private readonly RagDbContext _db;
    private readonly ITextDocumentIngestionService _ingestionService;

    public IngestionRunProcessor(
        RagDbContext db,
        ITextDocumentIngestionService ingestionService)
    {
        _db = db;
        _ingestionService = ingestionService;
    }
    public async Task<IngestionRunResult?> ProcessAsync(Guid ingestionRunId, CancellationToken cancellationToken)
    {
        var run = await _db.IngestionRuns
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == ingestionRunId, cancellationToken);

        if (run is null)
        {
            return null;
        }

        if (run.Status != IngestionRunStatus.Pending)
        {
            return ToResult(run);
        }

        try
        {
            run.Status = IngestionRunStatus.Running;
            run.StartedAt ??= DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            await _ingestionService.IngestPathAsync(
                run.Id,
                run.KnowledgeBaseId,
                run.DataSourceId,
                run.SourcePath,
                cancellationToken);

            run.Status = IngestionRunStatus.Completed;
            run.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            run.Status = IngestionRunStatus.Failed;
            run.ErrorMessage = ex.Message;
            run.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        await _db.Entry(run)
            .Collection(x => x.Items)
            .LoadAsync();

        return ToResult(run);
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
