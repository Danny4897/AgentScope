using AgentScope.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentScope.Infrastructure.Persistence.Configurations;

public sealed class HitlReviewConfiguration : IEntityTypeConfiguration<HitlReview>
{
    public void Configure(EntityTypeBuilder<HitlReview> builder)
    {
        builder.ToTable("HitlReviews");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.TraceId).IsRequired();
        builder.Property(h => h.ApplicationId).IsRequired();
        builder.Property(h => h.Action).IsRequired().HasMaxLength(500);
        builder.Property(h => h.Payload).HasMaxLength(8000);
        builder.Property(h => h.Status).HasConversion<int>().IsRequired();
        builder.Property(h => h.ReviewNote).HasMaxLength(2000);
        builder.Property(h => h.CreatedAt).IsRequired();

        builder.HasIndex(h => h.ApplicationId);
        builder.HasIndex(h => h.Status);
        builder.HasIndex(h => new { h.ApplicationId, h.Status });
    }
}
