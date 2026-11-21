using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SubTracker.Domain.Entities;

namespace SubTracker.DB.Configurations;

/// <summary>Fluent API-конфігурація центральної таблиці Subscriptions.</summary>
public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("Subscriptions", table =>
        {
            table.HasCheckConstraint("CK_Subscriptions_Amount", "[Amount] > 0");
            table.HasCheckConstraint("CK_Subscriptions_RemindDaysBefore", "[RemindDaysBefore] BETWEEN 0 AND 30");
            table.HasCheckConstraint("CK_Subscriptions_Currency", "LEN([Currency]) = 3");
        });

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100);

        // Гроші — тільки decimal з явною точністю: 16 цифр до коми, 2 після.
        builder.Property(s => s.Amount)
            .HasPrecision(18, 2);

        builder.Property(s => s.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .IsFixedLength()
            .IsUnicode(false);

        builder.Property(s => s.Period)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(s => s.NextChargeDate).HasColumnType("date");
        builder.Property(s => s.EndDate).HasColumnType("date");

        // ---- Друга міграція (AddTrialEndDateAndStatusIndex) ----
        builder.Property(s => s.TrialEndDate).HasColumnType("date");

        // Більшість запитів фільтрує «Status = 'Active' AND NextChargeDate <= …»:
        // складений індекс покриває обидві умови (календар, нагадування, звіти).
        builder.HasIndex(s => new { s.Status, s.NextChargeDate })
            .HasDatabaseName("IX_Subscriptions_Status_NextChargeDate");

        // Concurrency token: SQL Server сам змінює rowversion при кожному UPDATE,
        // а EF Core додає його у WHERE під час UPDATE/DELETE.
        builder.Property(s => s.RowVersion)
            .IsRowVersion();

        // Бізнес-правило: у одного власника не може бути двох підписок з однаковою назвою.
        // Індекс також обслуговує зовнішній ключ OwnerId (він перший у складеному ключі).
        builder.HasIndex(s => new { s.OwnerId, s.Name })
            .IsUnique()
            .HasDatabaseName("UX_Subscriptions_Owner_Name");

        // Календар списань фільтрує і сортує за датою наступного списання.
        builder.HasIndex(s => s.NextChargeDate)
            .HasDatabaseName("IX_Subscriptions_NextChargeDate");

        // ---- Зв'язки та правила видалення ----

        // Видалення користувача видаляє його підписки (і далі каскадом — їхні платежі та учасників).
        builder.HasOne(s => s.Owner)
            .WithMany(u => u.OwnedSubscriptions)
            .HasForeignKey(s => s.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Категорію, яка використовується, видалити не можна — спершу треба перенести підписки.
        builder.HasOne(s => s.Category)
            .WithMany(c => c.Subscriptions)
            .HasForeignKey(s => s.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Картку, з якої оплачуються підписки, видалити не можна — інакше списання «зависнуть».
        // Restrict також прибирає другий каскадний шлях Users → PaymentMethods → Subscriptions,
        // який SQL Server не дозволяє (multiple cascade paths).
        builder.HasOne(s => s.PaymentMethod)
            .WithMany(p => p.Subscriptions)
            .HasForeignKey(s => s.PaymentMethodId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
