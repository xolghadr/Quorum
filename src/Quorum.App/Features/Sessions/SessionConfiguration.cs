using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Quorum.App.Features.Catalog;
using Quorum.App.Features.Groups;
using Quorum.App.SharedKernel;

namespace Quorum.App.Features.Sessions;

public sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> entity)
    {
        entity.HasKey(session => session.Id);
        entity.Property(session => session.Id)
            .HasConversion(id => id.Value, value => new SessionId(value))
            .ValueGeneratedNever();
        entity.Property(session => session.GroupId)
            .HasConversion(id => id.Value, value => new GroupId(value));
        entity.Property(session => session.CreatedByAccountId)
            .HasConversion(id => id.Value, value => new AccountId(value));
        entity.Property(session => session.FacilitatorMembershipId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                value => value.HasValue ? new MemberId(value.Value) : null);
        entity.HasIndex(session => new { session.GroupId, session.Number }).IsUnique();
        entity.HasOne<Group>()
            .WithMany()
            .HasForeignKey(session => session.GroupId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SessionAttendanceConfiguration : IEntityTypeConfiguration<SessionAttendance>
{
    public void Configure(EntityTypeBuilder<SessionAttendance> entity)
    {
        entity.Property(row => row.SessionId)
            .HasConversion(id => id.Value, value => new SessionId(value));
        entity.Property(row => row.MembershipId)
            .HasConversion(id => id.Value, value => new MemberId(value));
        entity.HasKey(row => new { row.SessionId, row.MembershipId });
        entity.HasOne<Session>()
            .WithMany()
            .HasForeignKey(row => row.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Membership>()
            .WithMany()
            .HasForeignKey(row => row.MembershipId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SessionVoterConfiguration : IEntityTypeConfiguration<SessionVoter>
{
    public void Configure(EntityTypeBuilder<SessionVoter> entity)
    {
        entity.Property(row => row.SessionId)
            .HasConversion(id => id.Value, value => new SessionId(value));
        entity.Property(row => row.MembershipId)
            .HasConversion(id => id.Value, value => new MemberId(value));
        entity.HasKey(row => new { row.SessionId, row.MembershipId });
        entity.HasOne<Session>()
            .WithMany()
            .HasForeignKey(row => row.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SessionVoteConfiguration : IEntityTypeConfiguration<SessionVote>
{
    public void Configure(EntityTypeBuilder<SessionVote> entity)
    {
        entity.Property(row => row.SessionId)
            .HasConversion(id => id.Value, value => new SessionId(value));
        entity.Property(row => row.VoterMembershipId)
            .HasConversion(id => id.Value, value => new MemberId(value));
        entity.Property(row => row.CandidateMembershipId)
            .HasConversion(id => id.Value, value => new MemberId(value));
        entity.HasKey(row => new { row.SessionId, row.VoterMembershipId });
        entity.HasOne<Session>()
            .WithMany()
            .HasForeignKey(row => row.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SessionTopicConfiguration : IEntityTypeConfiguration<SessionTopic>
{
    public void Configure(EntityTypeBuilder<SessionTopic> entity)
    {
        entity.Property(row => row.SessionId)
            .HasConversion(id => id.Value, value => new SessionId(value));
        entity.HasKey(row => new { row.SessionId, row.Position });
        entity.HasIndex(row => new { row.SessionId, row.TopicId }).IsUnique();
        entity.HasOne<Session>()
            .WithMany()
            .HasForeignKey(row => row.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Topic>()
            .WithMany()
            .HasForeignKey(row => row.TopicId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TopicProgressConfiguration : IEntityTypeConfiguration<TopicProgress>
{
    public void Configure(EntityTypeBuilder<TopicProgress> entity)
    {
        entity.HasKey(progress => progress.Id);
        entity.Property(progress => progress.Id).ValueGeneratedNever();
        entity.Property(progress => progress.GroupId)
            .HasConversion(id => id.Value, value => new GroupId(value));
        entity.HasIndex(progress => new { progress.GroupId, progress.TopicId }).IsUnique();
        entity.HasOne<Group>()
            .WithMany()
            .HasForeignKey(progress => progress.GroupId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Topic>()
            .WithMany()
            .HasForeignKey(progress => progress.TopicId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
