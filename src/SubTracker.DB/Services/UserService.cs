using Microsoft.EntityFrameworkCore;
using SubTracker.Domain;
using SubTracker.Domain.Entities;

namespace SubTracker.DB.Services;

/// <summary>EF Core CRUD для сутності User.</summary>
public sealed class UserService(IDbContextFactory<SubTrackerDbContext> contextFactory)
{
    /// <summary>READ: усі користувачі (read-only → AsNoTracking).</summary>
    public async Task<List<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Users
            .AsNoTracking()
            .OrderBy(u => u.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>READ: користувач за Id або null.</summary>
    public async Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    /// <summary>CREATE: новий користувач. E-mail має бути унікальним.</summary>
    public async Task<User> CreateAsync(string name, string email, CancellationToken cancellationToken = default)
    {
        Validate(name, email);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var user = new User { Name = name.Trim(), Email = email.Trim().ToLowerInvariant() };
        db.Users.Add(user); // стан Added

        try
        {
            await db.SaveChangesAsync(cancellationToken); // INSERT ... OUTPUT INSERTED.Id
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            throw new BusinessRuleException($"Користувач з e-mail «{user.Email}» вже існує.", ex);
        }

        return user;
    }

    /// <summary>UPDATE: зміна імені та e-mail. Повертає false, якщо користувача немає.</summary>
    public async Task<bool> UpdateAsync(int id, string name, string email, CancellationToken cancellationToken = default)
    {
        Validate(name, email);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.FindAsync([id], cancellationToken); // tracked-сутність
        if (user is null)
            return false;

        user.Name = name.Trim();
        user.Email = email.Trim().ToLowerInvariant();
        // Change Tracker сам визначає змінені властивості → UPDATE лише змінених стовпців.

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            throw new BusinessRuleException($"E-mail «{user.Email}» вже зайнятий іншим користувачем.", ex);
        }

        return true;
    }

    /// <summary>
    /// DELETE: видалення користувача. Його картки та власні підписки видаляються каскадом;
    /// якщо він учасник чужих спільних підписок — спрацьовує Restrict.
    /// </summary>
    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Users.FindAsync([id], cancellationToken);
        if (user is null)
            return false;

        db.Users.Remove(user); // стан Deleted

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbErrors.IsConstraintViolation(ex))
        {
            throw new BusinessRuleException(
                "Користувача не можна видалити: він учасник спільних підписок інших людей.", ex);
        }

        return true;
    }

    private static void Validate(string name, string email)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            throw new BusinessRuleException("Ім'я обов'язкове і не довше 100 символів.");

        var trimmed = email?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > 256 || !trimmed.Contains('@') || trimmed.StartsWith('@') || trimmed.EndsWith('@'))
            throw new BusinessRuleException("Некоректний e-mail.");
    }
}
