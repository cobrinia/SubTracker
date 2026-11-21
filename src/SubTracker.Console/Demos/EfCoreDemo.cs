using SubTracker.ConsoleApp.Infrastructure;
using SubTracker.DB.Dtos;
using SubTracker.DB.Queries;
using SubTracker.DB.Services;
using SubTracker.Domain;
using SubTracker.Domain.Entities;
using SubTracker.Domain.Enums;

namespace SubTracker.ConsoleApp.Demos;

/// <summary>Демонстрація EF Core: CRUD трьох сутностей, масові операції, бізнес-операція з транзакцією.</summary>
public sealed class EfCoreDemo(
    UserService users,
    PaymentMethodService paymentMethods,
    SubscriptionService subscriptions,
    FamilyPlanService familyPlans,
    SubscriptionQueries queries)
{
    // ======================= CRUD трьох сутностей =======================

    public async Task CrudAsync(CancellationToken ct)
    {
        Ui.Title("EF Core — CRUD для User, PaymentMethod, Subscription");

        // ---------- User ----------
        Ui.Section("User: CREATE (стан Added → INSERT)");
        var name = Input.ReadString("Ім'я нового користувача", 100, "Олена Демченко");
        var email = $"demo{DateTime.Now:yyyyMMddHHmmss}@example.com";
        var user = await users.CreateAsync(name, email, ct);
        Ui.Success($"Створено користувача Id={user.Id}, {user.Email}");

        Ui.Section("User: CREATE з наявним e-mail → унікальний індекс UX_Users_Email");
        try
        {
            await users.CreateAsync("Дублікат", "o.kovalenko@example.com", ct);
        }
        catch (BusinessRuleException ex)
        {
            Ui.Error(ex.Message);
        }

        Ui.Section("User: UPDATE (Change Tracker → UPDATE лише змінених стовпців)");
        await users.UpdateAsync(user.Id, name + " (оновлено)", email, ct);
        var reloaded = await users.GetByIdAsync(user.Id, ct);
        Ui.Info($"READ після UPDATE: Id={reloaded?.Id}, Name=«{reloaded?.Name}»");

        // ---------- PaymentMethod ----------
        Ui.Section("PaymentMethod: CREATE");
        var card = await paymentMethods.CreateAsync(user.Id, PaymentMethodType.Card, "Monobank Iron", "1111",
            DateOnly.FromDateTime(DateTime.Today).AddYears(2), ct);
        Ui.Success($"Створено картку Id={card.Id} «{card.Title}» *{card.Last4}");

        Ui.Section("PaymentMethod: UPDATE (перевипуск картки)");
        await paymentMethods.UpdateAsync(card.Id, "Monobank Iron (перевипуск)", DateOnly.FromDateTime(DateTime.Today).AddYears(4), ct);
        var cards = await paymentMethods.GetByUserAsync(user.Id, ct);
        Ui.Table(["Id", "Тип", "Назва", "Last4", "Дійсна до"],
            cards.Select(c => new[] { c.Id.ToString(), c.Type.ToString(), c.Title, c.Last4 ?? "—", Ui.Date(c.ExpiryDate) }));

        // ---------- Subscription ----------
        Ui.Section("Subscription: CREATE");
        var subscription = await subscriptions.CreateAsync(new Subscription
        {
            OwnerId = user.Id,
            CategoryId = 1,
            PaymentMethodId = card.Id,
            Name = "Sweet.tv Оптимальний",
            Amount = 99m,
            Currency = "UAH",
            Period = BillingPeriod.Monthly,
            NextChargeDate = DateOnly.FromDateTime(DateTime.Today).AddDays(7),
            RemindDaysBefore = 2
        }, ct);
        Ui.Success($"Створено підписку Id={subscription.Id}; RowVersion від сервера: {Ui.Hex(subscription.RowVersion)}");

        Ui.Section("Subscription: READ з Include (Owner, Category, PaymentMethod)");
        PrintDetails(await subscriptions.GetDetailsAsync(subscription.Id, ct));

        Ui.Section("Subscription: UPDATE (сума, період, статус)");
        await subscriptions.UpdateAsync(subscription.Id, 1099m, BillingPeriod.Yearly, SubscriptionStatus.Active, ct);
        PrintDetails(await subscriptions.GetDetailsAsync(subscription.Id, ct));

        Ui.Section("PaymentMethod: DELETE картки, до якої прив'язана підписка → DeleteBehavior.Restrict");
        try
        {
            await paymentMethods.DeleteAsync(card.Id, ct);
        }
        catch (BusinessRuleException ex)
        {
            Ui.Error(ex.Message);
        }

        Ui.Section("DELETE у правильному порядку: Subscription → PaymentMethod → User");
        Ui.Success($"Subscription видалено: {await subscriptions.DeleteAsync(subscription.Id, ct)}");
        Ui.Success($"PaymentMethod видалено: {await paymentMethods.DeleteAsync(card.Id, ct)}");
        Ui.Success($"User видалено: {await users.DeleteAsync(user.Id, ct)}");
        Ui.Info($"READ після DELETE: {(await users.GetByIdAsync(user.Id, ct) is null ? "користувача немає" : "користувач є")}");

        Ui.Section("Масові операції ExecuteUpdate (без завантаження сутностей)");
        var paused = await subscriptions.PauseAllInCategoryAsync(2, ct);
        Ui.Success($"Призупинено активних підписок категорії «Музика»: {paused.Count} (один UPDATE ... WHERE)");
        var resumed = await subscriptions.ResumeAsync(paused, ct);
        Ui.Success($"Відновлено ті самі підписки: {resumed} (UPDATE ... WHERE Id IN (...)); призупинені раніше не зачеплено.");
    }

    private static void PrintDetails(Subscription? s)
    {
        if (s is null)
        {
            Ui.Warning("Підписку не знайдено.");
            return;
        }

        Ui.Table(["Id", "Назва", "Власник", "Категорія", "Картка", "Сума", "Період", "Статус", "Учасники"],
        [[
            s.Id.ToString(), s.Name, s.Owner.Name, s.Category.Name, s.PaymentMethod.Title,
            Ui.Money(s.Amount, s.Currency), s.Period.ToString(), s.Status.ToString(),
            s.Members.Count == 0 ? "—" : string.Join(", ", s.Members.Select(m => $"{m.User.Name} {m.SharePercent:0.##}%"))
        ]]);
    }

    // ======================= Бізнес-операція з entity graph і транзакцією =======================

    public async Task FamilyUpgradeAsync(CancellationToken ct)
    {
        Ui.Title("EF Core — бізнес-операція «Перехід на сімейний тариф» (entity graph + транзакція)");
        Ui.Muted("SaveChanges №1: стара підписка → Cancelled. SaveChanges №2: нова підписка + учасники + перший платіж + картка.");
        Ui.Muted("Обидва SaveChanges у ОДНІЙ транзакції: збій на кроці 2 скасовує і крок 1.");

        // Шукаємо власника з індивідуальною активною підпискою.
        List<SubscriptionListItemDto> candidates = [];
        var ownerId = 0;
        foreach (var id in new[] { 1, 3, 2, 5, 4 })
        {
            candidates = await queries.GetSoloActiveSubscriptionsAsync(id, ct);
            if (candidates.Count > 0) { ownerId = id; break; }
        }
        if (candidates.Count == 0)
        {
            Ui.Warning("Немає індивідуальних активних підписок для переведення.");
            return;
        }

        Ui.Section($"Індивідуальні активні підписки власника Id={ownerId}");
        Ui.Table(["Id", "Підписка", "Сума", "Період"],
            candidates.Select(c => new[] { c.Id.ToString(), c.Name, Ui.Money(c.Amount, c.Currency), c.Period.ToString() }));

        var chosenId = Input.ReadInt("Id підписки для переведення", 1, int.MaxValue, candidates[0].Id);
        var chosen = candidates.FirstOrDefault(c => c.Id == chosenId);
        if (chosen is null)
        {
            Ui.Error("Підписки з таким Id немає серед кандидатів.");
            return;
        }

        var partnerId = ownerId == 2 ? 1 : 2;
        var newName = chosen.Name.Length > 85 ? chosen.Name[..85] + " Family" : chosen.Name + " Family";
        var newAmount = decimal.Round(chosen.Amount * 1.5m, 2);

        // ---------- Сценарій 1: ROLLBACK ----------
        Ui.Section("Сценарій 1: серед учасників неіснуючий користувач Id=9999 → FK-помилка на SaveChanges №2 → ROLLBACK");
        try
        {
            await familyPlans.UpgradeToFamilyPlanAsync(new FamilyUpgradeRequest(
                chosen.Id, newName, newAmount, [new MemberShare(ownerId, 50m), new MemberShare(9999, 50m)]), ct);
        }
        catch (BusinessRuleException ex)
        {
            Ui.Error(ex.Message);
        }
        var afterRollback = await subscriptions.GetDetailsAsync(chosen.Id, ct);
        Ui.Warning($"Стара підписка після Rollback: статус {afterRollback?.Status} (крок 1 скасовано разом із кроком 2).");

        // ---------- Сценарій 2: перевірка бізнес-правила до транзакції ----------
        Ui.Section("Сценарій 2: сума часток 120 % → бізнес-правило, до БД не доходимо");
        try
        {
            await familyPlans.UpgradeToFamilyPlanAsync(new FamilyUpgradeRequest(
                chosen.Id, newName, newAmount, [new MemberShare(ownerId, 60m), new MemberShare(partnerId, 60m)]), ct);
        }
        catch (BusinessRuleException ex)
        {
            Ui.Error(ex.Message);
        }

        // ---------- Сценарій 3: COMMIT ----------
        Ui.Section("Сценарій 3: коректні дані → COMMIT");
        if (!Input.Confirm($"Перевести «{chosen.Name}» на «{newName}» ({Ui.Money(newAmount, chosen.Currency)}) для користувачів {ownerId} і {partnerId}?", true))
            return;

        var result = await familyPlans.UpgradeToFamilyPlanAsync(new FamilyUpgradeRequest(
            chosen.Id, newName, newAmount, [new MemberShare(ownerId, 50m), new MemberShare(partnerId, 50m)]), ct);

        Ui.Success($"COMMIT: нова підписка Id={result.NewSubscriptionId}, учасників {result.MembersCount}, " +
                   $"платіж Id={result.PaymentId}, наступне списання {Ui.Date(result.NextChargeDate)}");

        Ui.Info("Стара підписка:");
        PrintDetails(await subscriptions.GetDetailsAsync(result.OldSubscriptionId, ct));
        Ui.Info("Нова підписка:");
        PrintDetails(await subscriptions.GetDetailsAsync(result.NewSubscriptionId, ct));
    }
}
