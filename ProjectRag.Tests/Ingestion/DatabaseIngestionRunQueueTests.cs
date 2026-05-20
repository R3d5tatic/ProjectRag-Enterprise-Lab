using ProjectRag.Domain.Entities;
using ProjectRag.Domain.Enums;
using ProjectRag.Infrastructure.Ingestion;
using ProjectRag.Tests.Support;

namespace ProjectRag.Tests.Ingestion;

public sealed class DatabaseIngestionRunQueueTests
{
    [Fact]
    public async Task DequeuePendingRunAsync_returns_oldest_pending_run()
    {
        using var database = new SqliteTestDatabase();
        await using var db = database.CreateContext();

        var knowledgeBase = new KnowledgeBase
        {
            Id = Guid.NewGuid(),
            Name = "Default",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var dataSource = new DataSource
        {
            Id = Guid.NewGuid(),
            KnowledgeBaseId = knowledgeBase.Id,
            Name = "Source",
            SourceType = "localFile",
            SourceUri = "source.md",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var completed = Run(knowledgeBase.Id, dataSource.Id, IngestionRunStatus.Completed, DateTime.UtcNow.AddMinutes(-3));
        var newerPending = Run(knowledgeBase.Id, dataSource.Id, IngestionRunStatus.Pending, DateTime.UtcNow.AddMinutes(-1));
        var olderPending = Run(knowledgeBase.Id, dataSource.Id, IngestionRunStatus.Pending, DateTime.UtcNow.AddMinutes(-2));

        db.KnowledgeBases.Add(knowledgeBase);
        db.DataSources.Add(dataSource);
        db.IngestionRuns.AddRange(completed, newerPending, olderPending);
        await db.SaveChangesAsync();

        var queue = new DatabaseIngestionRunQueue(db);

        var result = await queue.DequeuePendingRunAsync(CancellationToken.None);

        Assert.Equal(olderPending.Id, result);
    }

    [Fact]
    public async Task DequeuePendingRunAsync_returns_null_when_no_pending_runs_exist()
    {
        using var database = new SqliteTestDatabase();
        await using var db = database.CreateContext();

        var queue = new DatabaseIngestionRunQueue(db);

        var result = await queue.DequeuePendingRunAsync(CancellationToken.None);

        Assert.Null(result);
    }

    private static IngestionRun Run(
        Guid knowledgeBaseId,
        Guid dataSourceId,
        IngestionRunStatus status,
        DateTime createdAt)
    {
        return new IngestionRun
        {
            Id = Guid.NewGuid(),
            KnowledgeBaseId = knowledgeBaseId,
            DataSourceId = dataSourceId,
            SourcePath = "source.md",
            Status = status,
            CreatedAt = createdAt
        };
    }
}
