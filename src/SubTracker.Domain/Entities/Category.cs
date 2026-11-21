namespace SubTracker.Domain.Entities;

/// <summary>Категорія підписки (довідник): стрімінг, ПЗ, зв'язок тощо.</summary>
public class Category
{
    public int Id { get; set; }

    /// <summary>Назва категорії — унікальна.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Підписки цієї категорії (1 → *).</summary>
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
