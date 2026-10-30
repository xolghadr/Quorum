using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Quorum.App.SharedKernel;

namespace Quorum.App.Features.Groups;

public sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> entity)
    {
        entity.HasKey(group => group.Id);
        entity.Property(group => group.Id)
            .HasConversion(id => id.Value, value => new GroupId(value))
            .ValueGeneratedNever();
        entity.Property(group => group.CreatorAccountId)
            .HasConversion(id => id.Value, value => new AccountId(value));
        entity.Property(group => group.Name).HasMaxLength(Group.NameMax).IsRequired();
        entity.HasIndex(group => group.CreatorAccountId);
        entity.HasOne<Accounts.Account>()
            .WithMany()
            .HasForeignKey(group => group.CreatorAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> entity)
    {
        entity.HasKey(member => member.Id);
        entity.Property(member => member.Id)
            .HasConversion(id => id.Value, value => new MemberId(value))
            .ValueGeneratedNever();
        entity.Property(member => member.GroupId)
            .HasConversion(id => id.Value, value => new GroupId(value));
        entity.Property(member => member.AccountId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                value => value.HasValue ? new AccountId(value.Value) : null);
        entity.Property(member => member.DisplayName).HasMaxLength(Membership.DisplayNameMax).IsRequired();
        entity.Property(member => member.DisplayNameKey).HasMaxLength(Membership.DisplayNameMax).IsRequired();
        entity.HasOne<Group>()
            .WithMany()
            .HasForeignKey(member => member.GroupId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(member => new { member.GroupId, member.DisplayNameKey })
            .IsUnique()
            .HasFilter("\"RemovedAt\" IS NULL");
        entity.HasIndex(member => new { member.GroupId, member.AccountId })
            .IsUnique()
            .HasFilter("\"AccountId\" IS NOT NULL AND \"RemovedAt\" IS NULL");
    }
}
