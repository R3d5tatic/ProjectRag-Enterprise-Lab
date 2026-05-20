using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectRag.Domain.Entities;

namespace ProjectRag.Infrastructure.Configurations.Persistence;

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceUri).HasMaxLength(2048);
        builder.Property(x => x.Title).HasMaxLength(512);
        builder.Property(x => x.ContentHash).HasMaxLength(128);
        builder.Property(x => x.SourceType).HasMaxLength(100);

        builder.HasOne(x => x.KnowledgeBase)
            .WithMany(x => x.Documents)
            .HasForeignKey(x => x.KnowledgeBaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DataSource)
            .WithMany(x => x.Documents)
            .HasForeignKey(x => x.DataSourceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.ContentHash);
        builder.HasIndex(x => x.KnowledgeBaseId);
        builder.HasIndex(x => x.DataSourceId);
    }
}
