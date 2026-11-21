using Microsoft.EntityFrameworkCore;
using SubTracker.Domain;
using SubTracker.Domain.Entities;
using SubTracker.Domain.Enums;

namespace SubTracker.DB.Services;

/// <summary>EF Core CRUD для сутності PaymentMethod.</summary>
public sealed class PaymentMethodService(IDbContextFactory<SubTrackerDbContext> contextFactory)
{
    /// <summary>READ: способи оплати користувача.</summary>
    public async Task<List<PaymentMethod>> GetByUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.PaymentMethods
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.Title)
            .ToListAsync(cancellationToken);
    }

    /// <summary>CREATE: новий спосіб оплати для існуючого користувача.</summary>
    public async Task<PaymentMethod> CreateAsync(
        int userId, PaymentMethodType type, string title, string? last4, DateOnly? expiryDate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 100)
            throw new BusinessRuleException("Назва способу оплати обов'язкова і не довша 100 символів.");
        if (last4 is not null && (last4.Length != 4 || !last4.All(char.IsAsciiDigit)))
            throw new BusinessRuleException("Last4 — рівно 4 цифри.");

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Users.AnyAsync(u => u.Id == userId, cancellationToken))
            throw new BusinessRuleException($"Користувача з Id={userId} не знайдено.");

        var method = new PaymentMethod
        {
            UserId = userId,
            Type = type,
            Title = title.Trim(),
            Last4 = last4,
            ExpiryDate = expiryDate
        };
        db.PaymentMethods.Add(method);
        await db.SaveChangesAsync(cancellationToken);
        return method;
    }

    /// <summary>UPDATE: перевипуск картки — нова назва і термін дії.</summary>
    public async Task<bool> UpdateAsync(int id, string title, DateOnly? expiryDate, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var method = await db.PaymentMethods.FindAsync([id], cancellationToken);
        if (method is null)
            return false;

        method.Title = title.Trim();
        method.ExpiryDate = expiryDate;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// DELETE: видалення способу оплати. DeleteBehavior.Restrict не дає видалити картку,
    /// до якої прив'язані підписки або з якої вже були платежі.
    /// </summary>
    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var method = await db.PaymentMethods.FindAsync([id], cancellationToken);
        if (method is null)
            return false;

        db.PaymentMethods.Remove(method);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbErrors.IsConstraintViolation(ex))
        {
            throw new BusinessRuleException(
                $"Спосіб оплати «{method.Title}» використовується підписками або має історію платежів — видалення заборонене.", ex);
        }

        return true;
    }
}
