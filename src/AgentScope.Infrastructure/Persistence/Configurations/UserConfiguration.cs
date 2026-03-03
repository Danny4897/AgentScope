using AgentScope.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentScope.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Email).HasMaxLength(320).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Tier).HasConversion<string>().HasMaxLength(20);
        builder.Property(u => u.CreatedAt).IsRequired();

        builder.HasMany(u => u.Applications)
            .WithOne()
            .HasForeignKey(a => a.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
