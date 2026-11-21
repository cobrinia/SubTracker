using Microsoft.EntityFrameworkCore;
using SubTracker.DB.Dtos;
using SubTracker.Domain;
using SubTracker.Domain.Billing;
using SubTracker.Domain.Entities;
using SubTracker.Domain.Enums;

namespace SubTracker.DB.Services;

/// <summary>
/// Складна бізнес-операція EF Core з entity graph у явній транзакції:
/// «Перевести індивідуальну підписку на сімейний тариф».
///
/// Кроки:
///   1) стару підписку скасувати (UPDATE Subscriptions)                        — SaveChanges №1;
///   2) створити граф: нова підписка + учасники + перший платіж,
///      оновити дату використання картки (INSERT ×3 таблиці + UPDATE PaymentMethods) — SaveChanges №2;
///   3) Commit.
/// Якщо крок 2 падає (неіснуючий учасник, дубль назви, CHECK), крок 1 теж скасовується:
/// стара підписка залишається активною. Без явної транзакції SaveChanges №1 уже був би зафіксований.
/// </summary>
public sealed class FamilyPlanService(IDbContextFactory<SubTrackerDbContext> contextFactory)
{
    public async Task<FamilyUpgradeResult> UpgradeToFamilyPlanAsync(
        FamilyUpgradeRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Завантажуємо стару підписку разом зі способом оплати (tracked — будемо змінювати).
            var old = await db.Subscriptions
                .Include(s => s.PaymentMethod)
                .FirstOrDefaultAsync(s => s.Id == request.SubscriptionId, cancellationToken)
                ?? throw new BusinessRuleException($"Підписку Id={request.SubscriptionId} не знайдено.");

            if (old.Status != SubscriptionStatus.Active)
                throw new BusinessRuleException($"Підписка «{old.Name}» не активна — переводити нічого.");
            if (!request.Members.Any(m => m.UserId == old.OwnerId))
                throw new BusinessRuleException("Власник підписки має бути серед учасників сімейного тарифу.");

            var today = DateOnly.FromDateTime(DateTime.Today);
            if (old.PaymentMethod.IsExpiredOn(today))
                throw new BusinessRuleException($"Картка «{old.PaymentMethod.Title}» прострочена.");

            // ---- Крок 1: скасувати стару підписку ----
            old.Status = SubscriptionStatus.Cancelled;
            old.EndDate = today;
            await db.SaveChangesAsync(cancellationToken);

            // ---- Крок 2: entity graph ----
            var now = DateTime.UtcNow;
            var family = new Subscription
            {
                OwnerId = old.OwnerId,
                CategoryId = old.CategoryId,
                PaymentMethod = old.PaymentMethod, // навігація на вже відстежувану сутність
                Name = request.NewPlanName.Trim(),
                Amount = request.NewAmount,
                Currency = old.Currency,
                Period = old.Period,
                NextChargeDate = BillingCalculator.NextDate(today, old.Period),
                Status = SubscriptionStatus.Active,
                RemindDaysBefore = old.RemindDaysBefore
            };

            foreach (var member in request.Members)
            {
                family.Members.Add(new SubscriptionMember
                {
                    UserId = member.UserId,
                    SharePercent = member.SharePercent,
                    JoinedAt = now
                });
            }

            // Перше списання за новим тарифом.
            var firstPayment = new Payment
            {
                PaymentMethod = old.PaymentMethod,
                ChargedAt = now,
                Amount = request.NewAmount,
                Currency = old.Currency,
                Status = PaymentStatus.Succeeded
            };
            family.Payments.Add(firstPayment);

            old.PaymentMethod.LastUsedAt = now;

            // Add() кореня графа переводить у стан Added усі нові сутності, досяжні через навігації.
            db.Subscriptions.Add(family);
            await db.SaveChangesAsync(cancellationToken);

            // ---- Крок 3: фіксація ----
            await transaction.CommitAsync(cancellationToken);

            return new FamilyUpgradeResult(old.Id, family.Id, firstPayment.Id, family.Members.Count, family.NextChargeDate);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);

            if (DbErrors.IsUniqueViolation(ex))
                throw new BusinessRuleException($"Підписка «{request.NewPlanName}» у власника вже існує. Зміни скасовано (Rollback).", ex);
            if (DbErrors.IsConstraintViolation(ex))
                throw new BusinessRuleException("Серед учасників є неіснуючий користувач або порушено обмеження БД. Зміни скасовано (Rollback).", ex);
            throw;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static void ValidateRequest(FamilyUpgradeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPlanName) || request.NewPlanName.Length > 100)
            throw new BusinessRuleException("Назва нового тарифу обов'язкова і не довша 100 символів.");
        if (request.NewAmount <= 0)
            throw new BusinessRuleException("Сума нового тарифу має бути більшою за нуль.");
        if (request.Members.Count < 2)
            throw new BusinessRuleException("Сімейний тариф — щонайменше два учасники.");
        if (request.Members.Select(m => m.UserId).Distinct().Count() != request.Members.Count)
            throw new BusinessRuleException("Учасники не можуть повторюватися.");
        if (request.Members.Any(m => m.SharePercent <= 0))
            throw new BusinessRuleException("Частка кожного учасника має бути більшою за нуль.");
        if (request.Members.Sum(m => m.SharePercent) != 100m)
            throw new BusinessRuleException("Сума часток учасників має дорівнювати 100 %.");
    }
}
