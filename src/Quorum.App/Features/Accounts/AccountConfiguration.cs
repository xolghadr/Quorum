using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Quorum.App.SharedKernel;

namespace Quorum.App.Features.Accounts;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> entity)
    {
        entity.HasKey(account => account.Id);
        entity.Property(account => account.Id)
            .HasConversion(id => id.Value, value => new AccountId(value))
            .ValueGeneratedNever();
        entity.Property(account => account.Email).HasMaxLength(320).IsRequired();
        entity.HasIndex(account => account.Email).IsUnique();
        entity.Property(account => account.PasswordHash).IsRequired();
        entity.Property(account => account.CreatedAt).IsRequired();
    }
}
