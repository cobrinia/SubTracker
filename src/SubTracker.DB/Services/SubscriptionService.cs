using Microsoft.EntityFrameworkCore;
using SubTracker.Domain;
using SubTracker.Domain.Entities;
using SubTracker.Domain.Enums;

namespace SubTracker.DB.Services;

/// <summary>EF Core CRUD для центральної сутності Subscription + масові операції.</summary>
public sealed class SubscriptionService(IDbContextFactory<SubTrackerDbContext> contextFactory)
{
    /// <summary>CREATE: нова підписка з перевіркою бізнес-правил.</summary>
    public async Task<Subscription> CreateAsync(Subscription subscription, CancellationToken cancellationToken = default)
    {
        Validate(subscription);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var method = await db.PaymentMethods
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == subscription.PaymentMethodId, cancellationToken)
            ?? throw new BusinessRuleException("Спосіб оплати не знайдено.");
        if (method.UserId != subscription.OwnerId)
            throw new BusinessRuleException("Спосіб оплати належить іншому користувачу.");

        db.Subscriptions.Add(subscription);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            throw new BusinessRuleException($"У власника вже є підписка «{subscription.Name}».", ex);
        }
        catch (DbUpdateException ex) when (DbErrors.IsConstraintViolation(ex))
        {
            throw new BusinessRuleException("Невірна категорія або власник підписки.", ex);
        }

        return subscription;
    }

    /// <summary>READ: підписка з пов'язаними даними (eager loading через Include).</summary>
    public async Task<Subscription?> GetDetailsAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Subscriptions
            .AsNoTracking()
            .Include(s => s.Owner)
            .Include(s => s.Category)
            .Include(s => s.PaymentMethod)
            .Include(s => s.Members).ThenInclude(m => m.User)
            .AsSplitQuery() // окремий запит для колекції Members — без «картезіанського вибуху»
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    /// <summary>UPDATE: зміна суми, періоду і статусу (tracked-сутність → Change Tracker).</summary>
    public async Task<bool> UpdateAsync(
        int id, decimal amount, BillingPeriod period, SubscriptionStatus status,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            throw new BusinessRuleException("Сума має бути більшою за нуль.");

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var subscription = await db.Subscriptions.FindAsync([id], cancellationToken);
        if (subscription is null)
            return false;

        subscription.Amount = amount;
        subscription.Period = period;
        subscription.Status = status;
        if (status == SubscriptionStatus.Cancelled)
            subscription.EndDate ??= DateOnly.FromDateTime(DateTime.Today);

        await db.SaveChangesAsync(cancellationToken); // WHERE Id = @p AND RowVersion = @p
        return true;
    }

    /// <summary>DELETE: каскадно видаляє також платежі та учасників підписки.</summary>
    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var subscription = await db.Subscriptions.FindAsync([id], cancellationToken);
        if (subscription is null)
            return false;

        db.Subscriptions.Remove(subscription);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Масова операція (розд. 25): призупинити всі активні підписки категорії одним UPDATE,
    /// без завантаження сутностей у пам'ять.
    /// </summary>
    /// <returns>Id призупинених підписок — щоб потім відновити саме їх.</returns>
    public async Task<IReadOnlyList<int>> PauseAllInCategoryAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var ids = await db.Subscriptions
            .Where(s => s.CategoryId == categoryId && s.Status == SubscriptionStatus.Active)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        await db.Subscriptions
            .Where(s => ids.Contains(s.Id))
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Status, SubscriptionStatus.Paused), cancellationToken);
        return ids;
    }

    /// <summary>Масова операція: відновити вказані призупинені підписки одним UPDATE ... WHERE Id IN (...).</summary>
    public async Task<int> ResumeAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Subscriptions
            .Where(s => ids.Contains(s.Id) && s.Status == SubscriptionStatus.Paused)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Status, SubscriptionStatus.Active), cancellationToken);
    }

    private static void Validate(Subscription s)
    {
        if (string.IsNullOrWhiteSpace(s.Name) || s.Name.Length > 100)
            throw new BusinessRuleException("Назва підписки обов'язкова і не довша 100 символів.");
        if (s.Amount <= 0)
            throw new BusinessRuleException("Сума має бути більшою за нуль.");
        if (s.Currency is not { Length: 3 } || !s.Currency.All(char.IsAsciiLetterUpper))
            throw new BusinessRuleException("Валюта — трилітерний код ISO, напр. UAH.");
        if (s.RemindDaysBefore is < 0 or > 30)
            throw new BusinessRuleException("Нагадування — від 0 до 30 днів.");
        if (s.EndDate is not null && s.EndDate < s.NextChargeDate)
            throw new BusinessRuleException("Дата завершення не може бути раніше за наступне списання.");
    }
}
