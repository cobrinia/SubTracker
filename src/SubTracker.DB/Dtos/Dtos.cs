using SubTracker.Domain.Enums;

namespace SubTracker.DB.Dtos;

/// <summary>Параметри динамічного фільтра підписок. null — фільтр не застосовується.</summary>
public sealed record SubscriptionFilter(
    SubscriptionStatus? Status = null,
    int? CategoryId = null,
    string? Currency = null,
    decimal? MinAmount = null,
    decimal? MaxAmount = null,
    string? NameContains = null);

/// <summary>Поле сортування списку підписок.</summary>
public enum SubscriptionSort { Name, Amount, NextChargeDate }

/// <summary>Рядок списку підписок (DTO-проєкція — тільки потрібні стовпці).</summary>
public sealed record SubscriptionListItemDto(
    int Id,
    string Name,
    string OwnerName,
    string CategoryName,
    decimal Amount,
    string Currency,
    BillingPeriod Period,
    DateOnly NextChargeDate,
    SubscriptionStatus Status);

/// <summary>Дані для календаря списань: одна активна підписка.</summary>
public sealed record ChargeScheduleDto(
    int SubscriptionId,
    string SubscriptionName,
    string OwnerName,
    string CategoryName,
    string PaymentMethodTitle,
    decimal Amount,
    string Currency,
    BillingPeriod Period,
    DateOnly NextChargeDate,
    DateOnly? EndDate,
    int RemindDaysBefore);

/// <summary>Одна подія календаря: конкретна дата конкретного списання.</summary>
public sealed record CalendarEntryDto(
    DateOnly Date,
    int SubscriptionId,
    string SubscriptionName,
    string OwnerName,
    string PaymentMethodTitle,
    decimal Amount,
    string Currency);

/// <summary>Агрегат «витрати за категорією у валюті».</summary>
public sealed record CategorySpendingDto(
    string CategoryName,
    string Currency,
    int ActiveCount,
    decimal MonthlyTotal,
    decimal YearlyTotal,
    decimal AverageAmount,
    decimal MaxAmount);

/// <summary>Агрегат «скільки користувач платить на місяць і рік у валюті».</summary>
public sealed record UserTotalsDto(
    int UserId,
    string UserName,
    string Currency,
    int SubscriptionCount,
    decimal MonthlyTotal,
    decimal YearlyTotal);

/// <summary>Нагадування про найближче списання.</summary>
public sealed record ReminderDto(
    int SubscriptionId,
    string SubscriptionName,
    string OwnerEmail,
    DateOnly NextChargeDate,
    decimal Amount,
    string Currency,
    int DaysLeft);

/// <summary>Сторінка результатів пагінації.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>Частка учасника спільної підписки.</summary>
public sealed record MemberShare(int UserId, decimal SharePercent);

/// <summary>Запит на переведення підписки в сімейний тариф.</summary>
public sealed record FamilyUpgradeRequest(
    int SubscriptionId,
    string NewPlanName,
    decimal NewAmount,
    IReadOnlyList<MemberShare> Members);

/// <summary>Результат переведення в сімейний тариф.</summary>
public sealed record FamilyUpgradeResult(
    int OldSubscriptionId,
    int NewSubscriptionId,
    int PaymentId,
    int MembersCount,
    DateOnly NextChargeDate);
