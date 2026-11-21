using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SubTracker.Domain.Entities;

namespace SubTracker.DB.Configurations;

/// <summary>
/// Fluent API-конфігурація проміжної сутності SubscriptionMembers (Users * ↔ * Subscriptions).
/// </summary>
public sealed class SubscriptionMemberConfiguration : IEntityTypeConfiguration<SubscriptionMember>
{
    public void Configure(EntityTypeBuilder<SubscriptionMember> builder)
    {
        builder.ToTable("SubscriptionMembers", table =>
        {
            table.HasCheckConstraint(
                "CK_SubscriptionMembers_SharePercent",
                "[SharePercent] > 0 AND [SharePercent] <= 100");
        });

        // Складений первинний ключ: користувач входить у підписку не більше одного разу.
        builder.HasKey(m => new { m.SubscriptionId, m.UserId });

        builder.Property(m => m.SharePercent).HasPrecision(5, 2);

        builder.Property(m => m.JoinedAt)
            .HasColumnType("datetime2(0)")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        // Підписку видалили — список її учасників більше не потрібен.
        builder.HasOne(m => m.Subscription)
            .WithMany(s => s.Members)
            .HasForeignKey(m => m.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Користувача, який бере участь у чужих спільних підписках, видалити не можна,
        // доки його не виключать з них. Крім того, Cascade тут створив би другий каскадний шлях
        // Users → Subscriptions → SubscriptionMembers, що заборонено в SQL Server.
        builder.HasOne(m => m.User)
            .WithMany(u => u.Memberships)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
