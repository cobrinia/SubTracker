using SubTracker.Domain.Enums;

namespace SubTracker.Domain.Entities;

/// <summary>
/// Підписка або інший регулярний платіж — центральна сутність предметної області.
/// </summary>
public class Subscription
{
    public int Id { get; set; }

    /// <summary>Власник (FK → Users).</summary>
    public int OwnerId { get; set; }

    /// <summary>Категорія (FK → Categories).</summary>
    public int CategoryId { get; set; }

    /// <summary>Спосіб оплати, з якого відбувається списання (FK → PaymentMethods).</summary>
    public int PaymentMethodId { get; set; }

    /// <summary>Назва сервісу/платежу, напр. "Netflix Premium". Унікальна в межах власника.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Сума одного списання.</summary>
    public decimal Amount { get; set; }

    /// <summary>Код валюти ISO 4217 (UAH, USD, EUR).</summary>
    public string Currency { get; set; } = "UAH";

    /// <summary>Періодичність списання.</summary>
    public BillingPeriod Period { get; set; }

    /// <summary>Дата наступного списання.</summary>
    public DateOnly NextChargeDate { get; set; }

    /// <summary>Дата завершення підписки; null — безстрокова.</summary>
    public DateOnly? EndDate { get; set; }

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

    /// <summary>
    /// Дата завершення пробного (безкоштовного) періоду; null — пробного періоду немає.
    /// Додано другою міграцією AddTrialEndDateAndStatusIndex (зміна моделі після першої версії).
    /// </summary>
    public DateOnly? TrialEndDate { get; set; }

    /// <summary>За скільки днів до списання нагадувати.</summary>
    public int RemindDaysBefore { get; set; } = 3;

    /// <summary>
    /// Concurrency token (SQL Server rowversion). Змінюється автоматично при кожному UPDATE
    /// і використовується для оптимістичної конкуренції.
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    // ---- Навігаційні властивості ----
    public User Owner { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public PaymentMethod PaymentMethod { get; set; } = null!;

    /// <summary>Історія списань (1 → *).</summary>
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    /// <summary>Учасники спільної підписки (частина зв'язку * ↔ *).</summary>
    public ICollection<SubscriptionMember> Members { get; set; } = new List<SubscriptionMember>();
}
