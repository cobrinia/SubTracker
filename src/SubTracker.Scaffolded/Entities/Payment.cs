using System;
using System.Collections.Generic;

namespace SubTracker.Scaffolded.Entities;

public partial class Payment
{
    public int Id { get; set; }

    public int SubscriptionId { get; set; }

    public int PaymentMethodId { get; set; }

    public DateTime ChargedAt { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = null!;

    public string Status { get; set; } = null!;

    public virtual PaymentMethod PaymentMethod { get; set; } = null!;

    public virtual Subscription Subscription { get; set; } = null!;
}
