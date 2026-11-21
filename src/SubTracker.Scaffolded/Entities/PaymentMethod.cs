using System;
using System.Collections.Generic;

namespace SubTracker.Scaffolded.Entities;

public partial class PaymentMethod
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Type { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Last4 { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();

    public virtual User User { get; set; } = null!;
}
