using SubTracker.Domain.Enums;

namespace SubTracker.Domain.Entities;

/// <summary>Факт списання коштів за підписку (історія платежів).</summary>
public class Payment
{
    public int Id { get; set; }

    /// <summary>За яку підписку списано (FK → Subscriptions).</summary>
    public int SubscriptionId { get; set; }

    /// <summary>З якого способу оплати списано (FK → PaymentMethods).</summary>
    public int PaymentMethodId { get; set; }

    /// <summary>Момент списання (UTC).</summary>
    public DateTime ChargedAt { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "UAH";

    public PaymentStatus Status { get; set; }

    // ---- Навігаційні властивості ----
    public Subscription Subscription { get; set; } = null!;
    public PaymentMethod PaymentMethod { get; set; } = null!;
}
