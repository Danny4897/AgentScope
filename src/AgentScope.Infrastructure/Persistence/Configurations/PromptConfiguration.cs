using AgentScope.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentScope.Infrastructure.Persistence.Configurations;

public sealed class PromptConfiguration : IEntityTypeConfiguration<Prompt>
{
    public void Configure(EntityTypeBuilder<Prompt> builder)
    {
        builder.ToTable("prompts");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.ApplicationId).HasColumnName("application_id").IsRequired();
        builder.Property(p => p.Slug).HasColumnName("slug").HasMaxLength(200).IsRequired();
        builder.Property(p => p.Version).HasColumnName("version").IsRequired();
        builder.Property(p => p.Content).HasColumnName("content").IsRequired();
        builder.Property(p => p.ChangeNote).HasColumnName("change_note").HasMaxLength(500);
        builder.Property(p => p.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(p => p.PublishedAt).HasColumnName("published_at").IsRequired();

        // Unique constraint: one active version per (app, slug)
        builder.HasIndex(p => new { p.ApplicationId, p.Slug, p.Version })
               .IsUnique()
               .HasDatabaseName("ix_prompts_app_slug_version");

        builder.HasIndex(p => new { p.ApplicationId, p.Slug, p.IsActive })
               .HasDatabaseName("ix_prompts_app_slug_active");
    }
}
