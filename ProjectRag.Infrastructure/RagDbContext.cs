using Microsoft.EntityFrameworkCore;
using ProjectRag.Domain.Entities;

namespace ProjectRag.Infrastructure;

public sealed class RagDbContext : DbContext
{
    public RagDbContext(DbContextOptions<RagDbContext> options) :
        base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<IngestionRun> IngestionRuns => Set<IngestionRun>();
    public DbSet<KnowledgeBase> KnowledgeBases => Set<KnowledgeBase>();
    public DbSet<DataSource> DataSources => Set<DataSource>();

    public DbSet<IngestionItem> IngestionItems => Set<IngestionItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RagDbContext).Assembly);
    }
}
