using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Quorum.App.Infrastructure.Logging;

public sealed class ActivityConfiguration : IEntityTypeConfiguration<ActivityRecord>
{
    public void Configure(EntityTypeBuilder<ActivityRecord> entity)
    {
        entity.ToTable("activity_records", table =>
        {
            table.HasCheckConstraint(
                "CK_activity_records_outcome",
                "\"Outcome\" IN ('succeeded', 'refused')");
            table.HasCheckConstraint(
                "CK_activity_records_reason",
                "\"Reason\" IS NULL OR \"Reason\" = 'unknown-account'");
            table.HasCheckConstraint(
                "CK_activity_records_duration",
                "\"DurationMs\" >= 0");
            table.HasCheckConstraint(
                "CK_activity_records_storage_wait",
                "\"StorageWaitMs\" >= 0 AND \"StorageWaitMs\" <= \"DurationMs\"");
            table.HasCheckConstraint(
                "CK_activity_records_counts",
                "(\"Attempt\" = 'publish-catalog' AND \"Outcome\" = 'succeeded') OR (\"Tracks\" IS NULL AND \"Topics\" IS NULL AND \"Designs\" IS NULL)");
        });
        entity.HasKey(row => row.Id);
        entity.Property(row => row.Id).ValueGeneratedNever();
        entity.Property(row => row.Reference).HasMaxLength(8).IsRequired();
        entity.Property(row => row.Attempt).HasMaxLength(32).IsRequired();
        entity.Property(row => row.Outcome).HasMaxLength(16).IsRequired();
        entity.Property(row => row.Reason).HasMaxLength(32);
        entity.Property(row => row.AcceptedAt).IsRequired();
        entity.Property(row => row.DurationMs).IsRequired();
        entity.Property(row => row.StorageWaitMs).IsRequired();
        entity.HasIndex(row => new { row.Reference, row.Attempt }).IsUnique();
        entity.HasIndex(row => row.AcceptedAt);
        entity.HasIndex(row => row.ExportedAt).HasFilter("\"ExportedAt\" IS NULL");
    }
}
