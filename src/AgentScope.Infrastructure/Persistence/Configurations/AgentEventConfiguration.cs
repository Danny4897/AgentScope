using AgentScope.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentScope.Infrastructure.Persistence.Configurations;

public sealed class AgentEventConfiguration : IEntityTypeConfiguration<AgentEvent>
{
    public void Configure(EntityTypeBuilder<AgentEvent> builder)
    {
        builder.ToTable("agent_events");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.SpanId);
        builder.HasIndex(e => e.OccurredAt);
        builder.Property(e => e.Name).HasMaxLength(256).IsRequired();
        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.AttributesJson).HasColumnType("jsonb").IsRequired();
    }
}
