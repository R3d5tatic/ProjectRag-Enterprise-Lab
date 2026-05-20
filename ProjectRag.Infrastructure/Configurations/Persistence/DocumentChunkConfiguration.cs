using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectRag.Domain.Entities;

namespace ProjectRag.Infrastructure.Configurations.Persistence;

internal sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Text).IsRequired();
        builder.Property(x => x.SectionTitle).HasMaxLength(512);
        builder.Property(x => x.LayoutRole).HasMaxLength(128);
        builder.Property(x => x.BoundingRegionsJson);

        builder.HasOne(x => x.Document)
            .WithMany(x => x.Chunks)
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.DocumentId, x.ChunkIndex }).IsUnique();
    }
}