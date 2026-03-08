using AgentScope.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentScope.Infrastructure.Persistence.Configurations;

public sealed class MetricPointConfiguration : IEntityTypeConfiguration<MetricPoint>
{
    public void Configure(EntityTypeBuilder<MetricPoint> builder)
    {
        builder.ToTable("metric_points");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.MetricName).HasMaxLength(256).IsRequired();
        builder.Property(m => m.Unit).HasMaxLength(64).IsRequired();
        builder.Property(m => m.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.TimeWindow).HasMaxLength(10).IsRequired();
        builder.Property(m => m.AttributesJson).HasColumnType("jsonb").IsRequired();

        // Ignore computed property
        builder.Ignore(m => m.Avg);

        builder.HasIndex(m => m.ApplicationId);
        builder.HasIndex(m => new { m.ApplicationId, m.MetricName, m.TimeWindow, m.WindowStart });
        builder.HasIndex(m => m.WindowStart);
    }
}
