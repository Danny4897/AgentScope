using AgentScope.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentScope.Infrastructure.Persistence.Configurations;

public sealed class EvaluationConfiguration : IEntityTypeConfiguration<Evaluation>
{
    public void Configure(EntityTypeBuilder<Evaluation> builder)
    {
        builder.ToTable("Evaluations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TraceId).IsRequired();
        builder.Property(e => e.ApplicationId).IsRequired();
        builder.Property(e => e.Score).HasConversion<int>().IsRequired();
        builder.Property(e => e.Label).HasMaxLength(200);
        builder.Property(e => e.Note).HasMaxLength(2000);
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasIndex(e => e.TraceId).IsUnique();
        builder.HasIndex(e => e.ApplicationId);
    }
}
