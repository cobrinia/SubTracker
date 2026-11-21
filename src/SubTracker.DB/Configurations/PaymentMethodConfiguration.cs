using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SubTracker.Domain.Entities;

namespace SubTracker.DB.Configurations;

/// <summary>Fluent API-конфігурація таблиці PaymentMethods.</summary>
public sealed class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.ToTable("PaymentMethods", table =>
        {
            // Last4 — рівно чотири цифри або NULL.
            table.HasCheckConstraint(
                "CK_PaymentMethods_Last4",
                "[Last4] IS NULL OR (LEN([Last4]) = 4 AND [Last4] NOT LIKE '%[^0-9]%')");
        });

        builder.HasKey(p => p.Id);

        // Enum зберігається рядком: дані в БД читабельні і для ADO.NET, і в SSMS.
        builder.Property(p => p.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(p => p.Title)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Last4)
            .HasMaxLength(4)
            .IsFixedLength()
            .IsUnicode(false);

        builder.Property(p => p.ExpiryDate).HasColumnType("date");
        builder.Property(p => p.LastUsedAt).HasColumnType("datetime2(0)");

        // Власник видаляється → його способи оплати видаляються разом із ним:
        // без власника картка не має сенсу.
        builder.HasOne(p => p.User)
            .WithMany(u => u.PaymentMethods)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
