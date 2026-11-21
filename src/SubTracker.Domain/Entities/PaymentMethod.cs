using SubTracker.Domain.Enums;

namespace SubTracker.Domain.Entities;

/// <summary>Спосіб оплати користувача: банківська картка, PayPal, Google Pay тощо.</summary>
public class PaymentMethod
{
    public int Id { get; set; }

    /// <summary>Власник способу оплати (FK → Users).</summary>
    public int UserId { get; set; }

    public PaymentMethodType Type { get; set; }

    /// <summary>Назва, зрозуміла користувачу, напр. "Monobank Black".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Останні 4 цифри картки (для карток), інакше null.</summary>
    public string? Last4 { get; set; }

    /// <summary>Дата завершення дії картки; null — безстроковий спосіб оплати.</summary>
    public DateOnly? ExpiryDate { get; set; }

    /// <summary>Коли спосіб оплати востаннє використовувався (UTC).</summary>
    public DateTime? LastUsedAt { get; set; }

    // ---- Навігаційні властивості ----
    public User User { get; set; } = null!;
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    /// <summary>Чи прострочений спосіб оплати на вказану дату.</summary>
    public bool IsExpiredOn(DateOnly date) => ExpiryDate is not null && ExpiryDate.Value < date;
}
