using Microsoft.EntityFrameworkCore;
using SubTracker.Domain.Billing;
using SubTracker.Domain.Entities;
using SubTracker.Domain.Enums;

namespace SubTracker.DB.Seeding;

/// <summary>
/// Тестові дані транзакційних таблиць (PaymentMethods, Subscriptions, SubscriptionMembers, Payments).
/// Викликається з UseSeeding/UseAsyncSeeding після застосування міграцій.
/// Дати рахуються відносно сьогоднішнього дня, щоб календар списань завжди мав майбутні події.
/// Сідінг ідемпотентний: якщо дані вже є — нічого не робить.
/// </summary>
public static class DataSeeder
{
    public static void Seed(DbContext context)
    {
        if (context.Set<PaymentMethod>().Any())
            return;

        context.AddRange(BuildSubscriptions(DateOnly.FromDateTime(DateTime.Today)));
        context.SaveChanges();
    }

    public static async Task SeedAsync(DbContext context, CancellationToken cancellationToken)
    {
        if (await context.Set<PaymentMethod>().AnyAsync(cancellationToken))
            return;

        context.AddRange(BuildSubscriptions(DateOnly.FromDateTime(DateTime.Today)));
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Будує граф об'єктів: підписки з навігаціями на способи оплати, учасників і платежі.
    /// EF Core обходить граф і вставляє всі нові сутності в правильному порядку (спершу батьківські).
    /// </summary>
    private static List<Subscription> BuildSubscriptions(DateOnly today)
    {
        DateOnly EndOfMonth(DateOnly d) => new(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month));

        // ---- Способи оплати (7) ----
        var monoBlack = Card(1, "Monobank Black", "4821", EndOfMonth(today.AddYears(2)));
        var googlePay = new PaymentMethod { UserId = 1, Type = PaymentMethodType.GooglePay, Title = "Google Pay" };
        var privat = Card(2, "ПриватБанк Універсальна", "1934", EndOfMonth(today.AddYears(1)));
        var payPal = new PaymentMethod { UserId = 3, Type = PaymentMethodType.PayPal, Title = "PayPal" };
        var monoWhite = Card(3, "Monobank White", "7710", EndOfMonth(today.AddMonths(8)));
        // Прострочена картка — для демонстрації Rollback.
        var oschad = Card(4, "Ощадбанк Visa", "5502", EndOfMonth(today.AddMonths(-2)));
        var sense = Card(5, "Sense Bank", "3087", EndOfMonth(today.AddYears(3)));

        // ---- Підписки (17) ----
        var subscriptions = new List<Subscription>
        {
            Sub(1, 1, monoBlack, "Netflix Premium",          449m,  "UAH", BillingPeriod.Monthly,   today.AddDays(5)),
            Sub(1, 2, monoBlack, "Spotify Premium Family",   269m,  "UAH", BillingPeriod.Monthly,   today.AddDays(12)),
            Sub(1, 4, googlePay, "Google One 2 ТБ",          9.99m, "USD", BillingPeriod.Monthly,   today.AddDays(3)),
            Sub(1, 5, monoBlack, "Київстар Безлім",          300m,  "UAH", BillingPeriod.Monthly,   today.AddDays(1), remind: 1),
            Sub(2, 6, privat,    "Duolingo Super",           1999m, "UAH", BillingPeriod.Yearly,    today.AddDays(60), remind: 7),
            Sub(2, 2, privat,    "Apple Music",              5.99m, "USD", BillingPeriod.Monthly,   today.AddDays(20), SubscriptionStatus.Paused),
            Sub(2, 8, privat,    "ОСББ «Затишок»",           1250m, "UAH", BillingPeriod.Monthly,   today.AddDays(9)),
            Sub(2, 6, privat,    "Журнал «Forbes Україна»",  450m,  "UAH", BillingPeriod.Quarterly, today.AddDays(30), end: today.AddDays(400)),
            Sub(3, 3, payPal,    "JetBrains All Products",   289m,  "USD", BillingPeriod.Yearly,    today.AddDays(120), remind: 14),
            Sub(3, 3, monoWhite, "GitHub Copilot",           10m,   "USD", BillingPeriod.Monthly,   today.AddDays(8)),
            Sub(3, 3, monoWhite, "Microsoft 365 Family",     3499m, "UAH", BillingPeriod.Yearly,    today.AddDays(45), remind: 7),
            Sub(4, 1, oschad,    "MEGOGO Максимальна",       199m,  "UAH", BillingPeriod.Monthly,   today.AddDays(2)),
            Sub(4, 7, oschad,    "Sport Life абонемент",     1450m, "UAH", BillingPeriod.Monthly,   today.AddDays(-30), SubscriptionStatus.Cancelled, end: today.AddDays(-30)),
            Sub(5, 6, sense,     "Coursera Plus",            59m,   "USD", BillingPeriod.Monthly,   today.AddDays(15), SubscriptionStatus.Paused),
            Sub(5, 5, sense,     "Домашній інтернет Тріолан", 250m, "UAH", BillingPeriod.Monthly,   today.AddDays(15)),
            Sub(5, 4, sense,     "Dropbox Plus",             11.99m, "USD", BillingPeriod.Monthly,  today.AddDays(20)),
            Sub(5, 8, sense,     "Доставка води",            180m,  "UAH", BillingPeriod.Weekly,    today.AddDays(4))
        };

        // ---- Учасники спільних підписок (7) ----
        var joined = DateTime.UtcNow.AddMonths(-3);
        AddMembers(subscriptions[0], joined, (1, 50m), (2, 50m));            // Netflix: подружжя
        AddMembers(subscriptions[1], joined, (1, 34m), (2, 33m), (5, 33m));  // Spotify Family
        AddMembers(subscriptions[10], joined, (3, 50m), (4, 50m));           // Microsoft 365 Family

        // ---- Історія платежів (45) ----
        foreach (var subscription in subscriptions)
            AddPaymentHistory(subscription);

        // Останній платіж MEGOGO не пройшов (картка прострочена), Apple Music — повернення коштів.
        subscriptions[11].Payments.OrderByDescending(p => p.ChargedAt).First().Status = PaymentStatus.Failed;
        subscriptions[5].Payments.OrderByDescending(p => p.ChargedAt).First().Status = PaymentStatus.Refunded;

        // Дата останнього використання способу оплати = дата його останнього платежу.
        foreach (var method in subscriptions.Select(s => s.PaymentMethod).Distinct())
        {
            method.LastUsedAt = subscriptions
                .SelectMany(s => s.Payments)
                .Where(p => p.PaymentMethod == method)
                .Select(p => (DateTime?)p.ChargedAt)
                .Max();
        }

        return subscriptions;
    }

    private static PaymentMethod Card(int userId, string title, string last4, DateOnly expiry) => new()
    {
        UserId = userId,
        Type = PaymentMethodType.Card,
        Title = title,
        Last4 = last4,
        ExpiryDate = expiry
    };

    private static Subscription Sub(
        int ownerId, int categoryId, PaymentMethod method, string name, decimal amount, string currency,
        BillingPeriod period, DateOnly nextChargeDate,
        SubscriptionStatus status = SubscriptionStatus.Active, DateOnly? end = null, int remind = 3) => new()
    {
        OwnerId = ownerId,
        CategoryId = categoryId,
        PaymentMethod = method,
        Name = name,
        Amount = amount,
        Currency = currency,
        Period = period,
        NextChargeDate = nextChargeDate,
        EndDate = end,
        Status = status,
        RemindDaysBefore = remind
    };

    private static void AddMembers(Subscription subscription, DateTime joinedAt, params (int UserId, decimal Share)[] members)
    {
        foreach (var (userId, share) in members)
            subscription.Members.Add(new SubscriptionMember { UserId = userId, SharePercent = share, JoinedAt = joinedAt });
    }

    /// <summary>Генерує кілька попередніх списань, відраховуючи періоди назад від NextChargeDate.</summary>
    private static void AddPaymentHistory(Subscription subscription)
    {
        var count = subscription.Period switch
        {
            BillingPeriod.Weekly => 4,
            BillingPeriod.Monthly => 3,
            BillingPeriod.Quarterly => 2,
            _ => 1
        };

        var date = subscription.NextChargeDate;
        for (var i = 0; i < count; i++)
        {
            date = BillingCalculator.PreviousDate(date, subscription.Period);
            subscription.Payments.Add(new Payment
            {
                PaymentMethod = subscription.PaymentMethod,
                ChargedAt = DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(9, 15)), DateTimeKind.Utc),
                Amount = subscription.Amount,
                Currency = subscription.Currency,
                Status = PaymentStatus.Succeeded
            });
        }
    }
}
