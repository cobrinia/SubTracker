using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SubTracker.DB.Seeding;
using SubTracker.Domain.Entities;

namespace SubTracker.DB.Configurations;

/// <summary>Fluent API-конфігурація довідника Categories.</summary>
public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(50);

        // Назва категорії не може повторюватися.
        builder.HasIndex(c => c.Name)
            .IsUnique()
            .HasDatabaseName("UX_Categories_Name");

        // Довідник заповнюється через HasData (потрапляє в міграцію).
        builder.HasData(SeedData.Categories());
    }
}
