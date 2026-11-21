using Microsoft.EntityFrameworkCore;
using SubTracker.AdoNet.Repositories;
using SubTracker.ConsoleApp.Infrastructure;
using SubTracker.DB;
using SubTracker.DB.Queries;
using SubTracker.DB.Services;

namespace SubTracker.ConsoleApp.Demos;

/// <summary>Початковий стан даних і стан міграцій.</summary>
public sealed class StateDemo(
    IDbContextFactory<SubTrackerDbContext> contextFactory,
    SubscriptionQueries queries,
    UserService users,
    CategoryRepository categories)
{
    public async Task ShowInitialStateAsync(CancellationToken ct)
    {
        Ui.Title("ПОЧАТКОВИЙ СТАН ДАНИХ");

        Ui.Section("Кількість записів у таблицях");
        var counts = await queries.GetTableCountsAsync(ct);
        Ui.Table(["Таблиця", "Записів"], counts.Select(kv => new[] { kv.Key, kv.Value.ToString() }));
        Ui.Muted($"Довідникові/головні (Users + Categories): {counts["Users"] + counts["Categories"]}; " +
                 $"транзакційні (PaymentMethods + Subscriptions + SubscriptionMembers + Payments): " +
                 $"{counts["PaymentMethods"] + counts["Subscriptions"] + counts["SubscriptionMembers"] + counts["Payments"]}");

        Ui.Section("Користувачі (EF Core, AsNoTracking)");
        var userList = await users.GetAllAsync(ct);
        Ui.Table(["Id", "Ім'я", "E-mail", "Зареєстрований"],
            userList.Select(u => new[] { u.Id.ToString(), u.Name, u.Email, Ui.DateTimeText(u.CreatedAt) }));

        Ui.Section("Категорії (ADO.NET, LEFT JOIN + COUNT)");
        var categoryList = await categories.GetAllAsync(ct);
        Ui.Table(["Id", "Категорія", "Підписок"],
            categoryList.Select(c => new[] { c.Id.ToString(), c.Name, c.SubscriptionCount.ToString() }));

        Ui.Section("Усі підписки (EF Core, DTO-проєкція)");
        var page = await queries.GetPageAsync(1, 100, ct);
        Ui.Table(["Id", "Підписка", "Власник", "Категорія", "Сума", "Період", "Наступне", "Статус"],
            page.Items.Select(s => new[]
            {
                s.Id.ToString(), s.Name, s.OwnerName, s.CategoryName, Ui.Money(s.Amount, s.Currency),
                s.Period.ToString(), Ui.Date(s.NextChargeDate), s.Status.ToString()
            }));
    }

    public async Task ShowMigrationsAsync(CancellationToken ct)
    {
        Ui.Title("МІГРАЦІЇ");
        await using var db = await contextFactory.CreateDbContextAsync(ct);

        var all = db.Database.GetMigrations().ToList();
        var applied = (await db.Database.GetAppliedMigrationsAsync(ct)).ToHashSet();

        Ui.Table(["Міграція", "Стан"],
            all.Select(m => new[] { m, applied.Contains(m) ? "застосована" : "ОЧІКУЄ" }));

        Ui.Muted("Список також видно командою: dotnet ef migrations list --project src/SubTracker.DB --startup-project src/SubTracker.Console");
    }
}
