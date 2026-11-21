namespace SubTracker.Domain.Entities;

/// <summary>Користувач системи: власник підписок або учасник спільних підписок.</summary>
public class User
{
    public int Id { get; set; }

    /// <summary>Ім'я та прізвище.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Електронна пошта — бізнес-ідентифікатор, унікальна в межах системи.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Дата реєстрації (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    // ---- Навігаційні властивості ----

    /// <summary>Способи оплати користувача (1 → *).</summary>
    public ICollection<PaymentMethod> PaymentMethods { get; set; } = new List<PaymentMethod>();

    /// <summary>Підписки, власником яких є користувач (1 → *).</summary>
    public ICollection<Subscription> OwnedSubscriptions { get; set; } = new List<Subscription>();

    /// <summary>Участь у спільних підписках (частина зв'язку * ↔ *).</summary>
    public ICollection<SubscriptionMember> Memberships { get; set; } = new List<SubscriptionMember>();
}
