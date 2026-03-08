using AgentScope.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentScope.Infrastructure.Persistence.Configurations;

public sealed class TokenUsageConfiguration : IEntityTypeConfiguration<TokenUsage>
{
    public void Configure(EntityTypeBuilder<TokenUsage> builder)
    {
        builder.ToTable("token_usages");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Model).HasMaxLength(100).IsRequired();
        builder.Property(t => t.CostUsd).HasPrecision(18, 8).IsRequired();
        builder.Property(t => t.RecordedAt).IsRequired();

        builder.HasIndex(t => t.ApplicationId);
        builder.HasIndex(t => t.RecordedAt);
        builder.Ignore(t => t.TotalTokens);
    }
}
