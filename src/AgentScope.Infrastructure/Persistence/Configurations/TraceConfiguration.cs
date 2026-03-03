using AgentScope.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentScope.Infrastructure.Persistence.Configurations;

public sealed class TraceConfiguration : IEntityTypeConfiguration<Trace>
{
    public void Configure(EntityTypeBuilder<Trace> builder)
    {
        builder.ToTable("traces");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.TraceId).HasMaxLength(32).IsRequired();
        builder.HasIndex(t => t.TraceId);
        builder.HasIndex(t => t.ApplicationId);
        builder.HasIndex(t => t.StartedAt);     // range queries for retention purge
        builder.Property(t => t.RootSpanName).HasMaxLength(256).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.StartedAt).IsRequired();

        builder.HasMany(t => t.Spans)
            .WithOne()
            .HasForeignKey(s => s.TraceId)
            .OnDelete(DeleteBehavior.Cascade);

        // DurationMs is computed — do not map to a column
        builder.Ignore(t => t.DurationMs);
    }
}
