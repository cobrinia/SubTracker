using System;
using System.Collections.Generic;

namespace SubTracker.Scaffolded.Entities;

public partial class SubscriptionMember
{
    public int SubscriptionId { get; set; }

    public int UserId { get; set; }

    public decimal SharePercent { get; set; }

    public DateTime JoinedAt { get; set; }

    public virtual Subscription Subscription { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
