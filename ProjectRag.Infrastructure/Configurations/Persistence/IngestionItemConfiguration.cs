using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectRag.Domain.Entities;

namespace ProjectRag.Infrastructure.Configurations.Persistence;

internal sealed class IngestionItemConfiguration : IEntityTypeConfiguration<IngestionItem>
{
    public void Configure(EntityTypeBuilder<IngestionItem> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceUri).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.ErrorMessage).HasMaxLength(4000);

        builder.HasOne(x => x.IngestionRun)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.IngestionRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Document)
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.IngestionRunId);
        builder.HasIndex(x => x.DocumentId);
        builder.HasIndex(x => x.SourceUri);
    }
}