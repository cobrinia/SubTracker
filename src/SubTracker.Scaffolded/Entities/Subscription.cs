using System;
using System.Collections.Generic;

namespace SubTracker.Scaffolded.Entities;

public partial class Subscription
{
    public int Id { get; set; }

    public int OwnerId { get; set; }

    public int CategoryId { get; set; }

    public int PaymentMethodId { get; set; }

    public string Name { get; set; } = null!;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = null!;

    public string Period { get; set; } = null!;

    public DateOnly NextChargeDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string Status { get; set; } = null!;

    public int RemindDaysBefore { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public DateOnly? TrialEndDate { get; set; }

    public virtual Category Category { get; set; } = null!;

    public virtual User Owner { get; set; } = null!;

    public virtual PaymentMethod PaymentMethod { get; set; } = null!;

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<SubscriptionMember> SubscriptionMembers { get; set; } = new List<SubscriptionMember>();
}
