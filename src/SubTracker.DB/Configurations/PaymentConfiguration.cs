using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SubTracker.Domain.Entities;

namespace SubTracker.DB.Configurations;

/// <summary>Fluent API-конфігурація історії платежів Payments.</summary>
public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", table =>
        {
            table.HasCheckConstraint("CK_Payments_Amount", "[Amount] > 0");
        });

        builder.HasKey(p => p.Id);

        builder.Property(p => p.ChargedAt).HasColumnType("datetime2(0)");

        builder.Property(p => p.Amount).HasPrecision(18, 2);

        builder.Property(p => p.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .IsFixedLength()
            .IsUnicode(false);

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        // Історію платежів підписки найчастіше читають «за підпискою, за датою».
        // Індекс також обслуговує FK SubscriptionId.
        builder.HasIndex(p => new { p.SubscriptionId, p.ChargedAt })
            .HasDatabaseName("IX_Payments_Subscription_ChargedAt");

        // Історія належить підписці: видалили підписку — видалили її платежі.
        builder.HasOne(p => p.Subscription)
            .WithMany(s => s.Payments)
            .HasForeignKey(p => p.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Спосіб оплати, з якого вже були списання, видалити не можна (фінансова історія).
        builder.HasOne(p => p.PaymentMethod)
            .WithMany(m => m.Payments)
            .HasForeignKey(p => p.PaymentMethodId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
