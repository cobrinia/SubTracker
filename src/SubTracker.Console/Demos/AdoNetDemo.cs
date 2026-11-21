using SubTracker.AdoNet.Models;
using SubTracker.AdoNet.Reports;
using SubTracker.AdoNet.Repositories;
using SubTracker.AdoNet.Transactions;
using SubTracker.ConsoleApp.Infrastructure;
using SubTracker.Domain;
using SubTracker.Domain.Enums;

namespace SubTracker.ConsoleApp.Demos;

/// <summary>Демонстрація ADO.NET-частини: CRUD, складні запити, транзакція.</summary>
public sealed class AdoNetDemo(
    CategoryRepository categories,
    SubscriptionRepository subscriptions,
    ReportQueries reports,
    ChargeTransactionService charges)
{
    // ======================= CRUD Categories =======================

    public async Task CategoriesCrudAsync(CancellationToken ct)
    {
        Ui.Title("ADO.NET — CRUD для Categories");

        Ui.Section("READ: усі категорії");
        await PrintCategoriesAsync(ct);

        Ui.Section("CREATE: INSERT ... OUTPUT INSERTED.Id (ExecuteScalarAsync)");
        var name = Input.ReadString("Назва нової категорії", 50, "Ігри та розваги");
        int id;
        try
        {
            id = await categories.CreateAsync(name, ct);
            Ui.Success($"Створено категорію Id={id}");
        }
        catch (BusinessRuleException ex)
        {
            // Повторний запуск демо: категорія вже є — знаходимо її і продовжуємо.
            Ui.Warning(ex.Message);
            id = (await categories.GetAllAsync(ct)).First(c => c.Name == name.Trim()).Id;
        }

        Ui.Section("CREATE з дублем назви → UNIQUE-обмеження (помилка 2627/2601)");
        try
        {
            await categories.CreateAsync(name, ct);
        }
        catch (BusinessRuleException ex)
        {
            Ui.Error(ex.Message);
        }

        Ui.Section("READ: за Id (SqlDataReader, CommandBehavior.SingleRow)");
        var category = await categories.GetByIdAsync(id, ct);
        Ui.Info(category is null ? "не знайдено" : $"Id={category.Id}, Name=«{category.Name}», підписок: {category.SubscriptionCount}");

        Ui.Section("UPDATE: ExecuteNonQueryAsync → кількість змінених рядків");
        var newName = (name.Trim().Length > 40 ? name.Trim()[..40] : name.Trim()) + " (ред.)";
        var updated = await categories.UpdateAsync(id, newName, ct);
        Ui.Success($"UPDATE змінив рядок: {updated}; нова назва «{newName}»");

        Ui.Section("DELETE категорії з підписками → FK RESTRICT (помилка 547)");
        try
        {
            await categories.DeleteAsync(1, ct);
        }
        catch (BusinessRuleException ex)
        {
            Ui.Error(ex.Message);
        }

        Ui.Section("DELETE нової категорії");
        var deleted = await categories.DeleteAsync(id, ct);
        Ui.Success($"Видалено: {deleted}");

        Ui.Section("READ: після видалення");
        await PrintCategoriesAsync(ct);
    }

    private async Task PrintCategoriesAsync(CancellationToken ct)
    {
        var list = await categories.GetAllAsync(ct);
        Ui.Table(["Id", "Категорія", "Підписок"], list.Select(c => new[] { c.Id.ToString(), c.Name, c.SubscriptionCount.ToString() }));
    }

    // ======================= CRUD Subscriptions =======================

    public async Task SubscriptionsCrudAsync(CancellationToken ct)
    {
        Ui.Title("ADO.NET — CRUD для Subscriptions (пов'язана з Categories)");

        const int ownerId = 5;
        Ui.Section($"READ: підписки власника Id={ownerId} (INNER JOIN Categories)");
        var before = await subscriptions.GetByOwnerAsync(ownerId, ct);
        PrintSubscriptions(before);

        var paymentMethodId = before.FirstOrDefault()?.PaymentMethodId
            ?? throw new BusinessRuleException("У власника немає жодної підписки, щоб узяти спосіб оплати.");

        Ui.Section("CREATE: параметризований INSERT");
        var planName = $"Prometheus Plus {DateTime.Now:HHmmss}";
        var newId = await subscriptions.CreateAsync(new NewSubscription(
            OwnerId: ownerId,
            CategoryId: 6,
            PaymentMethodId: paymentMethodId,
            Name: planName,
            Amount: 199m,
            Currency: "UAH",
            Period: BillingPeriod.Monthly,
            NextChargeDate: DateOnly.FromDateTime(DateTime.Today).AddDays(10),
            EndDate: null,
            RemindDaysBefore: 2), ct);
        Ui.Success($"Створено підписку Id={newId} «{planName}»");

        Ui.Section("CREATE з недопустимою сумою → перевірка вводу до звернення до БД");
        try
        {
            await subscriptions.CreateAsync(new NewSubscription(ownerId, 6, paymentMethodId, "Помилкова", -5m, "UAH",
                BillingPeriod.Monthly, DateOnly.FromDateTime(DateTime.Today), null, 3), ct);
        }
        catch (BusinessRuleException ex)
        {
            Ui.Error(ex.Message);
        }

        Ui.Section("READ: за Id");
        var row = await subscriptions.GetByIdAsync(newId, ct)
            ?? throw new InvalidOperationException("Щойно створена підписка не знайдена.");
        PrintSubscriptions([row]);

        Ui.Section("UPDATE з перевіркою RowVersion (оптимістична конкуренція вручну)");
        var ok = await subscriptions.UpdateAsync(row.Id, 249m, SubscriptionStatus.Active, row.NextChargeDate, row.RowVersion, ct);
        Ui.Success($"UPDATE зі свіжою RowVersion {Ui.Hex(row.RowVersion)}: змінено = {ok}");

        var stale = await subscriptions.UpdateAsync(row.Id, 299m, SubscriptionStatus.Paused, row.NextChargeDate, row.RowVersion, ct);
        if (!stale)
            Ui.Warning("UPDATE зі СТАРОЮ RowVersion не змінив жодного рядка: запис уже оновив хтось інший — конфлікт виявлено.");

        var afterUpdate = await subscriptions.GetByIdAsync(row.Id, ct);
        if (afterUpdate is not null)
        {
            PrintSubscriptions([afterUpdate]);
            Ui.Muted($"RowVersion змінилась: {Ui.Hex(row.RowVersion)} → {Ui.Hex(afterUpdate.RowVersion)}");
        }

        Ui.Section("DELETE");
        var deleted = await subscriptions.DeleteAsync(row.Id, ct);
        Ui.Success($"Видалено: {deleted}; повторне читання → {(await subscriptions.GetByIdAsync(row.Id, ct) is null ? "null (запису немає)" : "запис є")}");
    }

    private static void PrintSubscriptions(IEnumerable<SubscriptionRow> rows) =>
        Ui.Table(["Id", "Назва", "Категорія", "Сума", "Період", "Наступне", "Статус", "RowVersion"],
            rows.Select(s => new[]
            {
                s.Id.ToString(), s.Name, s.CategoryName, Ui.Money(s.Amount, s.Currency), s.Period.ToString(),
                Ui.Date(s.NextChargeDate), s.Status.ToString(), Ui.Hex(s.RowVersion)
            }));

    // ======================= Складні запити =======================

    public async Task ComplexQueriesAsync(CancellationToken ct)
    {
        Ui.Title("ADO.NET — складні SQL-запити");

        Ui.Section("Запит 1: JOIN 4 таблиць (Subscriptions, Users, Categories, PaymentMethods) + WHERE + ORDER BY");
        var today = DateOnly.FromDateTime(DateTime.Today);
        var from = Input.ReadDate("Початок періоду", today);
        DateOnly to;
        while (true)
        {
            to = Input.ReadDate("Кінець періоду", from.AddDays(30));
            if (to >= from) break;
            Ui.Error("Кінець періоду не може бути раніше за початок.");
        }
        var currency = Input.ReadOptional("Валюта (UAH/USD/EUR)", 3)?.ToUpperInvariant();
        if (currency is not null && (currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper)))
        {
            Ui.Warning("Некоректний код валюти — фільтр за валютою не застосовано.");
            currency = null;
        }

        var upcoming = await reports.GetUpcomingChargesAsync(from, to, currency, ct);
        Ui.Table(["Дата", "Підписка", "Власник", "Категорія", "Картка", "Сума"],
            upcoming.Select(u => new[]
            {
                Ui.Date(u.NextChargeDate), u.SubscriptionName, u.OwnerName, u.CategoryName,
                u.PaymentMethodTitle, Ui.Money(u.Amount, u.Currency)
            }));

        Ui.Section("Запит 2: GROUP BY категорія + валюта; COUNT, SUM, AVG, MAX, MIN; HAVING");
        var minCount = Input.ReadInt("Мінімальна кількість підписок у групі (HAVING)", 1, 100, 1);
        var spending = await reports.GetSpendingByCategoryAsync(minCount, ct);
        Ui.Table(["Категорія", "Вал.", "К-сть", "На місяць", "На рік", "Середня", "Максимум", "Найближче"],
            spending.Select(s => new[]
            {
                s.CategoryName, s.Currency, s.ActiveCount.ToString(),
                Ui.Money(decimal.Round(s.MonthlyTotal, 2), s.Currency),
                Ui.Money(decimal.Round(s.MonthlyTotal * 12, 2), s.Currency),
                Ui.Money(decimal.Round(s.AverageAmount, 2), s.Currency),
                Ui.Money(s.MaxAmount, s.Currency),
                Ui.Date(s.NearestCharge)
            }));
    }

    // ======================= Транзакції =======================

    public async Task TransactionCommitAsync(CancellationToken ct)
    {
        Ui.Title("ADO.NET — транзакція «Провести списання» → COMMIT");
        var candidates = await PrintCandidatesAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.Today);

        var suggested = candidates.FirstOrDefault(c => c.CardExpiryDate is null || c.CardExpiryDate >= today);
        if (suggested is null)
        {
            Ui.Warning("Немає підписок з дійсною карткою.");
            return;
        }

        var id = ReadCandidateId(candidates, suggested.SubscriptionId);
        await RunChargeAsync(id, ct);
    }

    public async Task TransactionRollbackAsync(CancellationToken ct)
    {
        Ui.Title("ADO.NET — транзакція «Провести списання» → ROLLBACK");
        Ui.Muted("Сценарій: INSERT Payments і два UPDATE вже виконані, але «платіжний шлюз» відхиляє прострочену картку.");
        var candidates = await PrintCandidatesAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.Today);

        var suggested = candidates.FirstOrDefault(c => c.CardExpiryDate < today);
        if (suggested is null)
        {
            Ui.Warning("Немає підписок з простроченою карткою.");
            return;
        }

        var id = ReadCandidateId(candidates, suggested.SubscriptionId);
        await RunChargeAsync(id, ct);
    }

    private async Task<List<ChargeCandidateRow>> PrintCandidatesAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var candidates = await reports.GetChargeCandidatesAsync(ct);
        Ui.Table(["Id", "Підписка", "Сума", "Наступне", "Картка", "Дійсна до"],
            candidates.Select(c => new[]
            {
                c.SubscriptionId.ToString(), c.SubscriptionName, Ui.Money(c.Amount, c.Currency), Ui.Date(c.NextChargeDate),
                c.PaymentMethodTitle, c.CardExpiryDate < today ? $"{Ui.Date(c.CardExpiryDate)} ПРОСТРОЧЕНА" : Ui.Date(c.CardExpiryDate)
            }));
        return candidates;
    }

    private static int ReadCandidateId(List<ChargeCandidateRow> candidates, int suggested)
    {
        while (true)
        {
            var id = Input.ReadInt("Id підписки для списання", 1, int.MaxValue, suggested);
            if (candidates.Any(c => c.SubscriptionId == id))
                return id;
            Ui.Error("Такої активної підписки немає в списку.");
        }
    }

    private async Task RunChargeAsync(int subscriptionId, CancellationToken ct)
    {
        var before = await charges.GetStateAsync(subscriptionId, ct);
        PrintState("Стан ДО транзакції", before);

        try
        {
            var result = await charges.ProcessChargeAsync(subscriptionId, ct);
            Ui.Success($"COMMIT: платіж Id={result.PaymentId} на {Ui.Money(result.Amount, result.Currency)} за «{result.SubscriptionName}»; " +
                       $"наступне списання {Ui.Date(result.PreviousChargeDate)} → {Ui.Date(result.NewNextChargeDate)}, статус {result.NewStatus}");
        }
        catch (BusinessRuleException ex)
        {
            Ui.Error($"ROLLBACK: {ex.Message}");
        }

        var after = await charges.GetStateAsync(subscriptionId, ct);
        PrintState("Стан ПІСЛЯ транзакції", after);

        if (before == after)
            Ui.Warning("Жодна з трьох таблиць не змінилася — усі зміни транзакції скасовано.");
        else
            Ui.Success("Зміни зафіксовано в усіх трьох таблицях (Payments, Subscriptions, PaymentMethods).");
    }

    private static void PrintState(string title, ChargeStateSnapshot s)
    {
        Ui.Section(title);
        Ui.Table(["Payments (к-сть)", "Subscriptions.NextChargeDate", "Subscriptions.Status", "PaymentMethods.LastUsedAt"],
            [[s.PaymentsCount.ToString(), Ui.Date(s.NextChargeDate), s.Status?.ToString() ?? "—", Ui.DateTimeText(s.CardLastUsedAt)]]);
    }
}
