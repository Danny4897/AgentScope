using AgentScope.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentScope.Infrastructure.Persistence.Configurations;

public sealed class ApplicationConfiguration : IEntityTypeConfiguration<Application>
{
    public void Configure(EntityTypeBuilder<Application> builder)
    {
        builder.ToTable("applications");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Name).HasMaxLength(100).IsRequired();
        builder.Property(a => a.ApiKey).HasMaxLength(128).IsRequired();
        builder.HasIndex(a => a.ApiKey).IsUnique();
        builder.Property(a => a.Description).HasMaxLength(500);
        builder.Property(a => a.IsActive).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt).IsRequired();

        builder.HasMany(a => a.Traces)
            .WithOne()
            .HasForeignKey(t => t.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
