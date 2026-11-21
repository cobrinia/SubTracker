using System;
using System.Collections.Generic;

namespace SubTracker.Scaffolded.Entities;

public partial class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
