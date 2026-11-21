namespace SubTracker.Domain.Entities;

/// <summary>
/// Явна проміжна сутність зв'язку багато-до-багатьох Users ↔ Subscriptions.
/// Описує спільну (сімейну) підписку: хто з користувачів нею користується і яку частку платить.
/// Має складений первинний ключ (SubscriptionId, UserId) та власні атрибути.
/// </summary>
public class SubscriptionMember
{
    public int SubscriptionId { get; set; }

    public int UserId { get; set; }

    /// <summary>Частка оплати учасника, % (0 &lt; x ≤ 100).</summary>
    public decimal SharePercent { get; set; }

    /// <summary>Коли учасник приєднався (UTC).</summary>
    public DateTime JoinedAt { get; set; }

    // ---- Навігаційні властивості ----
    public Subscription Subscription { get; set; } = null!;
    public User User { get; set; } = null!;
}
