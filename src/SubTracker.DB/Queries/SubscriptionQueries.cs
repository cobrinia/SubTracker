using Microsoft.EntityFrameworkCore;
using SubTracker.DB.Dtos;
using SubTracker.Domain.Billing;
using SubTracker.Domain.Entities;
using SubTracker.Domain.Enums;

namespace SubTracker.DB.Queries;

/// <summary>
/// LINQ-запити EF Core (пункт 9 ходу роботи):
///  1. Filter            — фільтрація за кількома параметрами (динамічний IQueryable) + AsNoTracking;
///  2. GetSorted         — сортування за вибраним полем + AsNoTracking;
///  3. GetChargeCalendar — DTO-проєкція (календар майбутніх списань);
///  4. GetUserOverview   — запит із пов'язаними даними (Include/ThenInclude) + AsNoTracking;
///  5. GetSpendingByCategory / GetTotalsByUser — GroupBy з агрегатами;
///  6. GetPage           — пагінація (Skip/Take + загальна кількість);
///  7. GetDueReminders   — нагадування перед продовженням.
/// Методи Build*Query повертають IQueryable без виконання — їх же використовує
/// демонстрація ToQueryString(), тому показаний SQL точно відповідає виконаному.
/// </summary>
public sealed class SubscriptionQueries(IDbContextFactory<SubTrackerDbContext> contextFactory)
{
    // ===================== 1. Фільтрація за кількома параметрами =====================

    public static IQueryable<Subscription> BuildFilterQuery(SubTrackerDbContext db, SubscriptionFilter filter)
    {
        // Запит будується поступово: кожна умова додається лише якщо параметр заданий.
        // До виклику ToListAsync() жодного звернення до БД немає (відкладене виконання).
        IQueryable<Subscription> query = db.Subscriptions
            .AsNoTracking()
            .Include(s => s.Category);

        if (filter.Status is { } status)
            query = query.Where(s => s.Status == status);
        if (filter.CategoryId is { } categoryId)
            query = query.Where(s => s.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(filter.Currency))
            query = query.Where(s => s.Currency == filter.Currency);
        if (filter.MinAmount is { } min)
            query = query.Where(s => s.Amount >= min);
        if (filter.MaxAmount is { } max)
            query = query.Where(s => s.Amount <= max);
        if (!string.IsNullOrWhiteSpace(filter.NameContains))
            query = query.Where(s => s.Name.Contains(filter.NameContains));

        return query.OrderBy(s => s.Name);
    }

    public async Task<List<Subscription>> FilterAsync(SubscriptionFilter filter, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await BuildFilterQuery(db, filter).ToListAsync(cancellationToken);
    }

    // ===================== 2. Сортування =====================

    public async Task<List<Subscription>> GetSortedAsync(
        SubscriptionSort sortBy, bool descending, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<Subscription> query = db.Subscriptions
            .AsNoTracking()
            .Include(s => s.Owner)
            .Where(s => s.Status != SubscriptionStatus.Cancelled);

        // Другий ключ сортування (ThenBy) робить порядок детермінованим.
        query = (sortBy, descending) switch
        {
            (SubscriptionSort.Amount, false) => query.OrderBy(s => s.Amount).ThenBy(s => s.Name),
            (SubscriptionSort.Amount, true) => query.OrderByDescending(s => s.Amount).ThenBy(s => s.Name),
            (SubscriptionSort.NextChargeDate, false) => query.OrderBy(s => s.NextChargeDate).ThenBy(s => s.Name),
            (SubscriptionSort.NextChargeDate, true) => query.OrderByDescending(s => s.NextChargeDate).ThenBy(s => s.Name),
            (_, false) => query.OrderBy(s => s.Name),
            (_, true) => query.OrderByDescending(s => s.Name)
        };

        return await query.ToListAsync(cancellationToken);
    }

    // ===================== 3. DTO-проєкція: календар списань =====================

    public static IQueryable<ChargeScheduleDto> BuildScheduleQuery(SubTrackerDbContext db, DateOnly to)
    {
        // Select у DTO: SQL вибирає тільки потрібні стовпці, JOIN-и будуються з навігацій,
        // сутності не відстежуються (AsNoTracking не потрібен — це не сутності).
        return db.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active && s.NextChargeDate <= to)
            .OrderBy(s => s.NextChargeDate)
            .Select(s => new ChargeScheduleDto(
                s.Id,
                s.Name,
                s.Owner.Name,
                s.Category.Name,
                s.PaymentMethod.Title,
                s.Amount,
                s.Currency,
                s.Period,
                s.NextChargeDate,
                s.EndDate,
                s.RemindDaysBefore));
    }

    /// <summary>
    /// Календар майбутніх списань на період [from; to]. БД повертає активні підписки,
    /// а повторення (щотижня/щомісяця…) розгортаються в пам'яті доменною логікою.
    /// </summary>
    public async Task<List<CalendarEntryDto>> GetChargeCalendarAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var schedule = await BuildScheduleQuery(db, to).ToListAsync(cancellationToken);

        return schedule
            .SelectMany(s => BillingCalculator
                .Occurrences(s.NextChargeDate, s.Period, s.EndDate, from, to)
                .Select(date => new CalendarEntryDto(
                    date, s.SubscriptionId, s.SubscriptionName, s.OwnerName, s.PaymentMethodTitle, s.Amount, s.Currency)))
            .OrderBy(e => e.Date)
            .ThenBy(e => e.SubscriptionName)
            .ToList();
    }

    // ===================== 4. Пов'язані дані (eager loading) =====================

    /// <summary>
    /// Користувач з усіма його підписками, категоріями, способами оплати та участю в чужих підписках.
    /// Include завантажує все наперед — немає N+1 (окремого запиту на кожну підписку).
    /// AsSplitQuery розбиває на кілька запитів, щоб уникнути дублювання рядків через кілька колекцій.
    /// </summary>
    public async Task<User?> GetUserOverviewAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Users
            .AsNoTracking()
            .Include(u => u.PaymentMethods)
            .Include(u => u.OwnedSubscriptions).ThenInclude(s => s.Category)
            .Include(u => u.Memberships).ThenInclude(m => m.Subscription).ThenInclude(s => s.Owner)
            .AsSplitQuery()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    // ===================== 5. GroupBy з агрегатами =====================

    public static IQueryable<CategorySpendingDto> BuildSpendingByCategoryQuery(SubTrackerDbContext db)
    {
        return db.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active)
            // Спершу проєкція: місячний еквівалент рахується в SQL через CASE.
            .Select(s => new
            {
                CategoryName = s.Category.Name,
                s.Currency,
                s.Amount,
                Monthly = s.Period == BillingPeriod.Weekly ? s.Amount * 52m / 12m
                        : s.Period == BillingPeriod.Quarterly ? s.Amount / 3m
                        : s.Period == BillingPeriod.Yearly ? s.Amount / 12m
                        : s.Amount
            })
            .GroupBy(x => new { x.CategoryName, x.Currency })
            // Сортування — до фінальної проєкції в DTO: EF Core транслює ключ групи, а не властивість record'а.
            .OrderBy(g => g.Key.CategoryName)
            .ThenBy(g => g.Key.Currency)
            .Select(g => new CategorySpendingDto(
                g.Key.CategoryName,
                g.Key.Currency,
                g.Count(),
                g.Sum(x => x.Monthly),
                g.Sum(x => x.Monthly) * 12m,
                g.Average(x => x.Amount),
                g.Max(x => x.Amount)));
    }

    public async Task<List<CategorySpendingDto>> GetSpendingByCategoryAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await BuildSpendingByCategoryQuery(db).ToListAsync(cancellationToken);
    }

    /// <summary>Загальна сума на місяць і рік по кожному користувачу та валюті.</summary>
    public async Task<List<UserTotalsDto>> GetTotalsByUserAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active)
            .Select(s => new
            {
                s.OwnerId,
                OwnerName = s.Owner.Name,
                s.Currency,
                Monthly = s.Period == BillingPeriod.Weekly ? s.Amount * 52m / 12m
                        : s.Period == BillingPeriod.Quarterly ? s.Amount / 3m
                        : s.Period == BillingPeriod.Yearly ? s.Amount / 12m
                        : s.Amount
            })
            .GroupBy(x => new { x.OwnerId, x.OwnerName, x.Currency })
            .OrderBy(g => g.Key.OwnerId)
            .ThenBy(g => g.Key.Currency)
            .Select(g => new UserTotalsDto(
                g.Key.OwnerId,
                g.Key.OwnerName,
                g.Key.Currency,
                g.Count(),
                g.Sum(x => x.Monthly),
                g.Sum(x => x.Monthly) * 12m))
            .ToListAsync(cancellationToken);
    }

    // ===================== 6. Пагінація =====================

    public static IQueryable<SubscriptionListItemDto> BuildPageQuery(SubTrackerDbContext db, int pageNumber, int pageSize)
    {
        // Пагінація ОБОВ'ЯЗКОВО після стабільного сортування (OrderBy + унікальний ThenBy),
        // інакше SQL Server може повертати рядки в різному порядку і сторінки «перемішаються».
        return db.Subscriptions
            .OrderBy(s => s.NextChargeDate)
            .ThenBy(s => s.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SubscriptionListItemDto(
                s.Id, s.Name, s.Owner.Name, s.Category.Name,
                s.Amount, s.Currency, s.Period, s.NextChargeDate, s.Status));
    }

    public async Task<PagedResult<SubscriptionListItemDto>> GetPageAsync(
        int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) throw new ArgumentOutOfRangeException(nameof(pageNumber), "Номер сторінки ≥ 1.");
        if (pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize), "Розмір сторінки 1..100.");

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var total = await db.Subscriptions.CountAsync(cancellationToken);
        var items = await BuildPageQuery(db, pageNumber, pageSize).ToListAsync(cancellationToken);
        return new PagedResult<SubscriptionListItemDto>(items, pageNumber, pageSize, total);
    }

    // ===================== 7. Нагадування перед продовженням =====================

    public async Task<List<ReminderDto>> GetDueRemindersAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Нагадуємо, якщо до списання лишилося не більше RemindDaysBefore днів.
        var due = await db.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active
                        && s.NextChargeDate >= today
                        && s.NextChargeDate.AddDays(-s.RemindDaysBefore) <= today)
            .OrderBy(s => s.NextChargeDate)
            .Select(s => new { s.Id, s.Name, s.Owner.Email, s.NextChargeDate, s.Amount, s.Currency })
            .ToListAsync(cancellationToken);

        return due
            .Select(x => new ReminderDto(x.Id, x.Name, x.Email, x.NextChargeDate, x.Amount, x.Currency,
                x.NextChargeDate.DayNumber - today.DayNumber))
            .ToList();
    }

    /// <summary>Активні індивідуальні (без учасників) підписки власника — кандидати на сімейний тариф.</summary>
    public async Task<List<SubscriptionListItemDto>> GetSoloActiveSubscriptionsAsync(
        int ownerId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Subscriptions
            .Where(s => s.OwnerId == ownerId && s.Status == SubscriptionStatus.Active && !s.Members.Any())
            .OrderBy(s => s.Id)
            .Select(s => new SubscriptionListItemDto(
                s.Id, s.Name, s.Owner.Name, s.Category.Name,
                s.Amount, s.Currency, s.Period, s.NextChargeDate, s.Status))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Скільки записів у кожній таблиці (стан БД для демонстрації).</summary>
    public async Task<Dictionary<string, int>> GetTableCountsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return new Dictionary<string, int>
        {
            ["Users"] = await db.Users.CountAsync(cancellationToken),
            ["Categories"] = await db.Categories.CountAsync(cancellationToken),
            ["PaymentMethods"] = await db.PaymentMethods.CountAsync(cancellationToken),
            ["Subscriptions"] = await db.Subscriptions.CountAsync(cancellationToken),
            ["SubscriptionMembers"] = await db.SubscriptionMembers.CountAsync(cancellationToken),
            ["Payments"] = await db.Payments.CountAsync(cancellationToken)
        };
    }
}
