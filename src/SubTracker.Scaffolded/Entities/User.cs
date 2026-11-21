using System;
using System.Collections.Generic;

namespace SubTracker.Scaffolded.Entities;

public partial class User
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<PaymentMethod> PaymentMethods { get; set; } = new List<PaymentMethod>();

    public virtual ICollection<SubscriptionMember> SubscriptionMembers { get; set; } = new List<SubscriptionMember>();

    public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
