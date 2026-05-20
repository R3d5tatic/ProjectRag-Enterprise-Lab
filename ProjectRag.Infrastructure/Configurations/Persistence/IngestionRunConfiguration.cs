using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectRag.Domain.Entities;

namespace ProjectRag.Infrastructure.Configurations.Persistence;

internal sealed class IngestionRunConfiguration : IEntityTypeConfiguration<IngestionRun>
{
    public void Configure(EntityTypeBuilder<IngestionRun> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourcePath).HasMaxLength(2048);
        builder.Property(x => x.ErrorMessage).HasMaxLength(4000);

        builder.HasOne(x => x.KnowledgeBase)
            .WithMany(x => x.IngestionRuns)
            .HasForeignKey(x => x.KnowledgeBaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DataSource)
            .WithMany(x => x.IngestionRuns)
            .HasForeignKey(x => x.DataSourceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.KnowledgeBaseId);
        builder.HasIndex(x => x.DataSourceId);
    }
}
