using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SubTracker.DB.Seeding;
using SubTracker.Domain.Entities;

namespace SubTracker.DB.Configurations;

/// <summary>Fluent API-конфігурація таблиці Users.</summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        // Бізнес-ідентифікатор: один e-mail — один користувач.
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("UX_Users_Email");

        // Значення за замовчуванням генерує сервер, якщо застосунок його не передав.
        builder.Property(u => u.CreatedAt)
            .HasColumnType("datetime2(0)")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        // Головна таблиця заповнюється через HasData (потрапляє в міграцію).
        builder.HasData(SeedData.Users());
    }
}
