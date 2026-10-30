using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Quorum.App.Features.Catalog;

public sealed class CatalogPublicationConfiguration : IEntityTypeConfiguration<CatalogPublication>
{
    public void Configure(EntityTypeBuilder<CatalogPublication> entity)
    {
        entity.HasKey(publication => publication.Id);
        entity.Property(publication => publication.Id).ValueGeneratedNever();
        entity.Property(publication => publication.SourceKey).HasMaxLength(80).IsRequired();
        entity.Property(publication => publication.CompletedOn).IsRequired();
    }
}

public sealed class TrackConfiguration : IEntityTypeConfiguration<Track>
{
    public void Configure(EntityTypeBuilder<Track> entity)
    {
        entity.HasKey(track => track.Id);
        entity.Property(track => track.Id).ValueGeneratedNever();
        entity.Property(track => track.Key).HasMaxLength(120).IsRequired();
        entity.Property(track => track.Title).HasMaxLength(200).IsRequired();
        entity.HasIndex(track => track.Key).IsUnique();
        entity.HasOne<CatalogPublication>()
            .WithMany()
            .HasForeignKey(track => track.PublicationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_tracks_publication");
    }
}

public sealed class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    public void Configure(EntityTypeBuilder<Topic> entity)
    {
        entity.HasKey(topic => topic.Id);
        entity.Property(topic => topic.Id).ValueGeneratedNever();
        entity.Property(topic => topic.Key).HasMaxLength(200).IsRequired();
        entity.Property(topic => topic.Title).HasMaxLength(200).IsRequired();
        entity.Property(topic => topic.Level).HasMaxLength(40).IsRequired();
        entity.HasIndex(topic => topic.Key).IsUnique();
        entity.HasOne<Track>()
            .WithMany()
            .HasForeignKey(topic => topic.TrackId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TopicSectionConfiguration : IEntityTypeConfiguration<TopicSection>
{
    public void Configure(EntityTypeBuilder<TopicSection> entity)
    {
        entity.HasKey(section => section.Id);
        entity.Property(section => section.Id).ValueGeneratedNever();
        entity.Property(section => section.Key).HasMaxLength(40).IsRequired();
        entity.HasIndex(section => new { section.TopicId, section.Key }).IsUnique();
        entity.HasOne<Topic>()
            .WithMany()
            .HasForeignKey(section => section.TopicId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> entity)
    {
        entity.HasKey(question => question.Id);
        entity.Property(question => question.Id).ValueGeneratedNever();
        entity.Property(question => question.Kind).HasConversion<string>().HasMaxLength(16);
        entity.Property(question => question.Fingerprint).HasMaxLength(64).IsRequired();
        entity.HasIndex(question => new { question.TopicId, question.Fingerprint }).IsUnique();
        entity.HasOne<Topic>()
            .WithMany()
            .HasForeignKey(question => question.TopicId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WorkedDesignConfiguration : IEntityTypeConfiguration<WorkedDesign>
{
    public void Configure(EntityTypeBuilder<WorkedDesign> entity)
    {
        entity.HasKey(design => design.Id);
        entity.Property(design => design.Id).ValueGeneratedNever();
        entity.Property(design => design.Key).HasMaxLength(120).IsRequired();
        entity.Property(design => design.Title).HasMaxLength(200).IsRequired();
        entity.Property(design => design.Level).HasMaxLength(40).IsRequired();
        entity.HasIndex(design => design.Key).IsUnique();
        entity.HasOne<CatalogPublication>()
            .WithMany()
            .HasForeignKey(design => design.PublicationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_designs_publication");
    }
}
