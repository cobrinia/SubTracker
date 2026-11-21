using Microsoft.EntityFrameworkCore;
using SubTracker.ConsoleApp.Infrastructure;
using SubTracker.DB;
using SubTracker.DB.Diagnostics;
using SubTracker.DB.Dtos;
using SubTracker.DB.Queries;
using SubTracker.Domain.Enums;

namespace SubTracker.ConsoleApp.Demos;

/// <summary>SQL, який генерує EF Core: ToQueryString(), логування LogTo, перевірка відсутності N+1.</summary>
public sealed class SqlDiagnosticsDemo(IDbContextFactory<SubTrackerDbContext> contextFactory, SubscriptionQueries queries)
{
    public async Task RunAsync(CancellationToken ct)
    {
        Ui.Title("SQL, сформований EF Core");
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.Today);

        // ---------- ToQueryString ----------
        Ui.Section("Запит 1 — фільтрація (Active, UAH, сума 100..5000, Include Category)");
        Ui.Muted("Очікуємо: INNER JOIN Categories (Include), WHERE з параметрами @status, @min, @max, ORDER BY Name.");
        PrintSql(SubscriptionQueries.BuildFilterQuery(db,
            new SubscriptionFilter(SubscriptionStatus.Active, Currency: "UAH", MinAmount: 100m, MaxAmount: 5000m)).ToQueryString());

        Ui.Section("Запит 2 — GroupBy з агрегатами (витрати за категоріями)");
        Ui.Muted("Очікуємо: INNER JOIN Categories, CASE для місячної суми, GROUP BY Name, Currency, COUNT/SUM/AVG/MAX.");
        PrintSql(SubscriptionQueries.BuildSpendingByCategoryQuery(db).ToQueryString());

        Ui.Section("Запит 3 — пагінація, сторінка 2 по 5 (DTO-проєкція)");
        Ui.Muted("Очікуємо: SELECT лише потрібних стовпців, 2 JOIN-и з навігацій, ORDER BY + OFFSET/FETCH.");
        PrintSql(SubscriptionQueries.BuildPageQuery(db, 2, 5).ToQueryString());

        Ui.Section("Запит 4 — DTO-проєкція календаря (3 JOIN-и: Users, Categories, PaymentMethods)");
        PrintSql(SubscriptionQueries.BuildScheduleQuery(db, today.AddDays(30)).ToQueryString());

        // ---------- Логування + N+1 ----------
        Ui.Section("Логування SQL (LogTo, RelationalEventId.CommandExecuted): антипатерн N+1");
        int badCount;
        using (SqlLog.Capture())
        {
            // ПОГАНО: окремий запит на категорію КОЖНОЇ підписки.
            var subs = await db.Subscriptions.AsNoTracking().Where(s => s.OwnerId == 1).ToListAsync(ct);
            foreach (var s in subs)
                _ = await db.Categories.AsNoTracking().FirstAsync(c => c.Id == s.CategoryId, ct);
            badCount = SqlLog.CommandCount;
            Ui.Warning($"N+1: {subs.Count} підписок → {badCount} SQL-команд (1 + {subs.Count}).");
        }

        Ui.Section("Логування SQL: той самий результат з Include — один запит з JOIN");
        using (SqlLog.Capture())
        {
            var subs = await db.Subscriptions.AsNoTracking().Include(s => s.Category).Where(s => s.OwnerId == 1).ToListAsync(ct);
            Ui.Success($"Include: {subs.Count} підписок → {SqlLog.CommandCount} SQL-команда. N+1 немає.");
        }

        Ui.Section("Логування SQL демонстраційного запиту «огляд користувача» (Include ×3 + AsSplitQuery)");
        using (SqlLog.Capture())
        {
            var user = await queries.GetUserOverviewAsync(1, ct);
            Ui.Success($"Завантажено {user?.OwnedSubscriptions.Count} власних підписок і {user?.Memberships.Count} участей " +
                       $"за {SqlLog.CommandCount} SQL-команди — кількість фіксована і не залежить від обсягу даних.");
        }
    }

    private static void PrintSql(string sql)
    {
        Ui.Write(ConsoleColor.DarkCyan, sql);
    }
}
