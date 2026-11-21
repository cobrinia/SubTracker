using Microsoft.EntityFrameworkCore;
using SubTracker.Domain.Entities;

namespace SubTracker.DB;

/// <summary>
/// Контекст EF Core: одиниця роботи з базою даних SubTrackerDb.
/// Кожен DbSet відповідає таблиці. Конфігурація сутностей винесена
/// в окремі класи IEntityTypeConfiguration&lt;T&gt; (папка Configurations).
/// </summary>
public class SubTrackerDbContext(DbContextOptions<SubTrackerDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<SubscriptionMember> SubscriptionMembers => Set<SubscriptionMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Автоматично застосовує всі IEntityTypeConfiguration<T> з цієї збірки.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SubTrackerDbContext).Assembly);
    }
}
