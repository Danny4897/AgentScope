using AgentScope.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentScope.Infrastructure.Persistence.Configurations;

public sealed class SpanConfiguration : IEntityTypeConfiguration<Span>
{
    public void Configure(EntityTypeBuilder<Span> builder)
    {
        builder.ToTable("spans");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.SpanId).HasMaxLength(16).IsRequired();
        builder.HasIndex(s => s.SpanId);
        builder.HasIndex(s => s.TraceId);
        builder.Property(s => s.ParentSpanId).HasMaxLength(16);
        builder.Property(s => s.Name).HasMaxLength(256).IsRequired();
        builder.Property(s => s.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(10);
        builder.Property(s => s.StatusMessage).HasMaxLength(1024);
        builder.Property(s => s.StartedAt).IsRequired();
        builder.Property(s => s.EndedAt).IsRequired();
        builder.Property(s => s.AttributesJson).HasColumnType("jsonb").IsRequired();

        builder.HasMany(s => s.Events)
            .WithOne()
            .HasForeignKey(e => e.SpanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(s => s.DurationMs);
    }
}
