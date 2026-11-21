using SubTracker.ConsoleApp.Infrastructure;
using SubTracker.DB.Dtos;
using SubTracker.DB.Queries;
using SubTracker.Domain.Entities;
using SubTracker.Domain.Enums;

namespace SubTracker.ConsoleApp.Demos;

/// <summary>Демонстрація LINQ-запитів EF Core і пагінації.</summary>
public sealed class LinqDemo(SubscriptionQueries queries)
{
    public async Task RunAllAsync(CancellationToken ct)
    {
        Ui.Title("EF Core — LINQ-запити");
        var today = DateOnly.FromDateTime(DateTime.Today);

        // 1. Фільтрація за кількома параметрами
        Ui.Section("1. Фільтрація за кількома параметрами (динамічний IQueryable, AsNoTracking)");
        var filter = ReadFilter();
        var filtered = await queries.FilterAsync(filter, ct);
        PrintEntities(filtered);

        // 2. Сортування
        Ui.Section("2. Сортування: за сумою за спаданням (AsNoTracking, ThenBy для стабільності)");
        var sorted = await queries.GetSortedAsync(SubscriptionSort.Amount, descending: true, ct);
        Ui.Table(["Id", "Підписка", "Власник", "Сума", "Статус"],
            sorted.Select(s => new[] { s.Id.ToString(), s.Name, s.Owner.Name, Ui.Money(s.Amount, s.Currency), s.Status.ToString() }));

        // 3. DTO-проєкція — календар
        Ui.Section("3. DTO-проєкція: календар списань на 30 днів");
        var calendar = await queries.GetChargeCalendarAsync(today, today.AddDays(30), ct);
        Ui.Table(["Дата", "Підписка", "Власник", "Картка", "Сума"],
            calendar.Select(e => new[] { Ui.Date(e.Date), e.SubscriptionName, e.OwnerName, e.PaymentMethodTitle, Ui.Money(e.Amount, e.Currency) }));
        foreach (var total in calendar.GroupBy(e => e.Currency))
            Ui.Info($"Разом за 30 днів у {total.Key}: {Ui.Money(total.Sum(e => e.Amount), total.Key)}");

        // 4. Пов'язані дані
        Ui.Section("4. Запит із пов'язаними даними: користувач Id=1 (Include/ThenInclude, AsSplitQuery, AsNoTracking)");
        var user = await queries.GetUserOverviewAsync(1, ct);
        if (user is not null)
            PrintUserOverview(user);

        // 5. GroupBy
        Ui.Section("5a. GroupBy з агрегатами: витрати за категоріями (Count, Sum, Average, Max)");
        var spending = await queries.GetSpendingByCategoryAsync(ct);
        Ui.Table(["Категорія", "Вал.", "Активних", "На місяць", "На рік", "Середня", "Максимум"],
            spending.Select(s => new[]
            {
                s.CategoryName, s.Currency, s.ActiveCount.ToString(),
                Ui.Money(decimal.Round(s.MonthlyTotal, 2), s.Currency),
                Ui.Money(decimal.Round(s.YearlyTotal, 2), s.Currency),
                Ui.Money(decimal.Round(s.AverageAmount, 2), s.Currency),
                Ui.Money(s.MaxAmount, s.Currency)
            }));

        Ui.Section("5b. GroupBy: загальна сума на місяць і рік по користувачах");
        var totals = await queries.GetTotalsByUserAsync(ct);
        Ui.Table(["Користувач", "Вал.", "Підписок", "На місяць", "На рік"],
            totals.Select(t => new[]
            {
                t.UserName, t.Currency, t.SubscriptionCount.ToString(),
                Ui.Money(decimal.Round(t.MonthlyTotal, 2), t.Currency), Ui.Money(decimal.Round(t.YearlyTotal, 2), t.Currency)
            }));

        // 6. Пагінація
        Ui.Section("6. Пагінація: сторінка 1, розмір 5 (OrderBy + Skip/Take)");
        PrintPage(await queries.GetPageAsync(1, 5, ct));

        // 7. Нагадування
        Ui.Section("7. Нагадування перед продовженням (NextChargeDate − RemindDaysBefore ≤ сьогодні)");
        var reminders = await queries.GetDueRemindersAsync(today, ct);
        Ui.Table(["Підписка", "Кому", "Дата списання", "Днів", "Сума"],
            reminders.Select(r => new[] { r.SubscriptionName, r.OwnerEmail, Ui.Date(r.NextChargeDate), r.DaysLeft.ToString(), Ui.Money(r.Amount, r.Currency) }));
    }

    public async Task PaginationAsync(CancellationToken ct)
    {
        Ui.Title("EF Core — пагінація підписок");
        var pageSize = Input.ReadInt("Розмір сторінки", 1, 20, 5);
        var page = 1;

        while (true)
        {
            var result = await queries.GetPageAsync(page, pageSize, ct);
            PrintPage(result);

            if (result.TotalPages == 0)
                return;

            Console.Write("[н] наступна, [п] попередня, номер сторінки, [Enter] вихід: ");
            var command = (Console.ReadLine() ?? string.Empty).Trim().ToLowerInvariant();
            if (command.Length == 0)
                return;

            if (command is "н" or "n")
                page = Math.Min(page + 1, result.TotalPages);
            else if (command is "п" or "p")
                page = Math.Max(page - 1, 1);
            else if (int.TryParse(command, out var number) && number >= 1 && number <= result.TotalPages)
                page = number;
            else
                Ui.Error($"Невідома команда. Сторінки: 1..{result.TotalPages}.");
        }
    }

    private static SubscriptionFilter ReadFilter()
    {
        Ui.Muted("Enter — залишити значення за замовчуванням.");
        var statusIndex = Input.ReadInt("Статус: 0 — будь-який, 1 — Active, 2 — Paused, 3 — Cancelled", 0, 3, 1);
        var currency = Input.ReadOptional("Валюта (UAH/USD/EUR)", 3)?.ToUpperInvariant();
        if (currency is not null && (currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper)))
        {
            Ui.Warning("Некоректна валюта — фільтр не застосовано.");
            currency = null;
        }
        var min = Input.ReadDecimal("Мінімальна сума", 0m, 1_000_000m, 100m);
        decimal max;
        while (true)
        {
            max = Input.ReadDecimal("Максимальна сума", 0m, 1_000_000m, 5000m);
            if (max >= min) break;
            Ui.Error("Максимум не може бути меншим за мінімум.");
        }
        var nameContains = Input.ReadOptional("Назва містить", 50);

        return new SubscriptionFilter(
            Status: statusIndex == 0 ? null : (SubscriptionStatus)statusIndex,
            Currency: currency,
            MinAmount: min,
            MaxAmount: max,
            NameContains: nameContains);
    }

    private static void PrintEntities(IEnumerable<Subscription> list) =>
        Ui.Table(["Id", "Підписка", "Категорія", "Сума", "Період", "Наступне", "Статус"],
            list.Select(s => new[]
            {
                s.Id.ToString(), s.Name, s.Category.Name, Ui.Money(s.Amount, s.Currency),
                s.Period.ToString(), Ui.Date(s.NextChargeDate), s.Status.ToString()
            }));

    private static void PrintPage(PagedResult<SubscriptionListItemDto> page)
    {
        Ui.Table(["Id", "Підписка", "Власник", "Сума", "Наступне", "Статус"],
            page.Items.Select(s => new[]
            {
                s.Id.ToString(), s.Name, s.OwnerName, Ui.Money(s.Amount, s.Currency), Ui.Date(s.NextChargeDate), s.Status.ToString()
            }));
        Ui.Info($"Сторінка {page.PageNumber} з {page.TotalPages}; усього записів: {page.TotalCount}");
    }

    private static void PrintUserOverview(User user)
    {
        Ui.Info($"{user.Name} <{user.Email}>");
        Ui.Info("Способи оплати: " + string.Join("; ", user.PaymentMethods.Select(p => $"{p.Title}{(p.Last4 is null ? "" : " *" + p.Last4)}")));
        Ui.Table(["Власні підписки", "Категорія", "Сума", "Статус"],
            user.OwnedSubscriptions.OrderBy(s => s.Name)
                .Select(s => new[] { s.Name, s.Category.Name, Ui.Money(s.Amount, s.Currency), s.Status.ToString() }));
        Ui.Table(["Бере участь у", "Власник", "Частка"],
            user.Memberships.OrderBy(m => m.Subscription.Name)
                .Select(m => new[] { m.Subscription.Name, m.Subscription.Owner.Name, $"{m.SharePercent:0.##} %" }));
    }
}
