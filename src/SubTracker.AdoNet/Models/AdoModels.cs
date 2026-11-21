using SubTracker.Domain.Enums;

namespace SubTracker.AdoNet.Models;

/// <summary>Категорія з кількістю підписок (результат SELECT).</summary>
public sealed record CategoryRow(int Id, string Name, int SubscriptionCount);

/// <summary>Підписка, прочитана через SqlDataReader.</summary>
public sealed record SubscriptionRow(
    int Id,
    int OwnerId,
    int CategoryId,
    string CategoryName,
    int PaymentMethodId,
    string Name,
    decimal Amount,
    string Currency,
    BillingPeriod Period,
    DateOnly NextChargeDate,
    DateOnly? EndDate,
    SubscriptionStatus Status,
    int RemindDaysBefore,
    byte[] RowVersion);

/// <summary>Дані для створення підписки через ADO.NET.</summary>
public sealed record NewSubscription(
    int OwnerId,
    int CategoryId,
    int PaymentMethodId,
    string Name,
    decimal Amount,
    string Currency,
    BillingPeriod Period,
    DateOnly NextChargeDate,
    DateOnly? EndDate,
    int RemindDaysBefore);

/// <summary>Рядок звіту «майбутні списання» (JOIN 4 таблиць).</summary>
public sealed record UpcomingChargeRow(
    int SubscriptionId,
    string SubscriptionName,
    decimal Amount,
    string Currency,
    BillingPeriod Period,
    DateOnly NextChargeDate,
    string OwnerName,
    string CategoryName,
    string PaymentMethodTitle);

/// <summary>Рядок агрегованого звіту GROUP BY категорія + валюта.</summary>
public sealed record CategorySpendingRow(
    string CategoryName,
    string Currency,
    int ActiveCount,
    decimal MonthlyTotal,
    decimal AverageAmount,
    decimal MaxAmount,
    DateOnly NearestCharge);

/// <summary>Кандидат на списання (для вибору в демонстрації транзакції).</summary>
public sealed record ChargeCandidateRow(
    int SubscriptionId,
    string SubscriptionName,
    decimal Amount,
    string Currency,
    DateOnly NextChargeDate,
    string PaymentMethodTitle,
    DateOnly? CardExpiryDate);

/// <summary>Результат успішно проведеної транзакції списання.</summary>
public sealed record ChargeResult(
    int PaymentId,
    int SubscriptionId,
    string SubscriptionName,
    decimal Amount,
    string Currency,
    DateOnly PreviousChargeDate,
    DateOnly NewNextChargeDate,
    SubscriptionStatus NewStatus);

/// <summary>Знімок стану трьох таблиць, які змінює транзакція (для доказу Commit/Rollback).</summary>
public sealed record ChargeStateSnapshot(
    int PaymentsCount,
    DateOnly? NextChargeDate,
    SubscriptionStatus? Status,
    DateTime? CardLastUsedAt);
