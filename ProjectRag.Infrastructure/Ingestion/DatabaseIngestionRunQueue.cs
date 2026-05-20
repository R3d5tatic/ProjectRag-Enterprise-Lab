using Microsoft.EntityFrameworkCore;
using ProjectRag.Application.Abstractions;
using ProjectRag.Domain.Enums;

namespace ProjectRag.Infrastructure.Ingestion;

internal sealed class DatabaseIngestionRunQueue : IPendingIngestionRunQueue
{
    private readonly RagDbContext _db;

    public DatabaseIngestionRunQueue(RagDbContext db)
    {
        _db = db;
    }

    public async Task<Guid?> DequeuePendingRunAsync(CancellationToken cancellationToken)
    {
        var run = await _db.IngestionRuns
            .AsNoTracking()
            .Where(x => x.Status == IngestionRunStatus.Pending)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new { x.Id })
            .FirstOrDefaultAsync(cancellationToken);

        return run?.Id;
    }
}
