using SubTracker.Domain.Enums;

namespace SubTracker.Domain.Billing;

/// <summary>
/// Чиста бізнес-логіка розрахунку дат і сум. Не залежить від БД,
/// тому однаково використовується і в ADO.NET-, і в EF Core-частині.
/// </summary>
public static class BillingCalculator
{
    /// <summary>Дата наступного списання після <paramref name="date"/>.</summary>
    public static DateOnly NextDate(DateOnly date, BillingPeriod period) => period switch
    {
        BillingPeriod.Weekly => date.AddDays(7),
        BillingPeriod.Monthly => date.AddMonths(1),
        BillingPeriod.Quarterly => date.AddMonths(3),
        BillingPeriod.Yearly => date.AddYears(1),
        _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Невідома періодичність.")
    };

    /// <summary>Дата попереднього списання перед <paramref name="date"/>.</summary>
    public static DateOnly PreviousDate(DateOnly date, BillingPeriod period) => period switch
    {
        BillingPeriod.Weekly => date.AddDays(-7),
        BillingPeriod.Monthly => date.AddMonths(-1),
        BillingPeriod.Quarterly => date.AddMonths(-3),
        BillingPeriod.Yearly => date.AddYears(-1),
        _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Невідома періодичність.")
    };

    /// <summary>Еквівалент суми за один місяць (для звітів «скільки на місяць / рік»).</summary>
    public static decimal ToMonthly(decimal amount, BillingPeriod period) => period switch
    {
        BillingPeriod.Weekly => amount * 52m / 12m,
        BillingPeriod.Monthly => amount,
        BillingPeriod.Quarterly => amount / 3m,
        BillingPeriod.Yearly => amount / 12m,
        _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Невідома періодичність.")
    };

    /// <summary>
    /// Усі дати списань підписки в інтервалі [from; to] з урахуванням дати завершення.
    /// Використовується для побудови календаря майбутніх списань.
    /// </summary>
    public static IEnumerable<DateOnly> Occurrences(
        DateOnly nextChargeDate, BillingPeriod period, DateOnly? endDate, DateOnly from, DateOnly to)
    {
        var date = nextChargeDate;
        while (date <= to && (endDate is null || date <= endDate))
        {
            if (date >= from)
                yield return date;
            date = NextDate(date, period);
        }
    }
}
