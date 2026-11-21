using Microsoft.EntityFrameworkCore;
using SubTracker.ConsoleApp.Infrastructure;
using SubTracker.DB;
using SubTracker.Domain.Entities;

namespace SubTracker.ConsoleApp.Demos;

/// <summary>
/// Оптимістична конкуренція через rowversion: два незалежні DbContext («Користувач А» і «Користувач Б»)
/// читають ту саму підписку і змінюють її. Другий SaveChanges отримує DbUpdateConcurrencyException,
/// бо в WHERE UPDATE стоїть стара RowVersion.
/// </summary>
public sealed class ConcurrencyDemo(IDbContextFactory<SubTrackerDbContext> contextFactory)
{
    public async Task RunAsync(CancellationToken ct)
    {
        Ui.Title("Optimistic concurrency (rowversion) — два окремі DbContext");

        var id = Input.ReadInt("Id підписки", 1, int.MaxValue, 1);

        await using var contextA = await contextFactory.CreateDbContextAsync(ct);
        await using var contextB = await contextFactory.CreateDbContextAsync(ct);

        // Обидва «користувачі» читають ОДНАКОВИЙ стан рядка (однакова RowVersion).
        var subA = await contextA.Subscriptions.FirstOrDefaultAsync(s => s.Id == id, ct);
        var subB = await contextB.Subscriptions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (subA is null || subB is null)
        {
            Ui.Error($"Підписку Id={id} не знайдено.");
            return;
        }

        var originalAmount = subA.Amount;
        Ui.Info($"А і Б прочитали «{subA.Name}»: сума {Ui.Money(subA.Amount, subA.Currency)}, RowVersion {Ui.Hex(subA.RowVersion)}");

        // Користувач А зберігає першим — успішно.
        subA.Amount = originalAmount + 10m;
        await contextA.SaveChangesAsync(ct);
        Ui.Success($"А зберіг суму {Ui.Money(subA.Amount, subA.Currency)}. Нова RowVersion: {Ui.Hex(subA.RowVersion)}");

        // Користувач Б має застарілу RowVersion.
        subB.Amount = originalAmount + 20m;
        try
        {
            await contextB.SaveChangesAsync(ct);
            Ui.Warning("Конфлікт не виник (неочікувано).");
        }
        catch (DbUpdateConcurrencyException ex)
        {
            Ui.Error("Б: DbUpdateConcurrencyException — рядок змінив інший користувач після того, як Б його прочитав.");
            await ResolveAsync(ex, ct);
        }

        // Повертаємо початкову суму, щоб демо можна було повторювати на тих самих даних.
        await using var cleanup = await contextFactory.CreateDbContextAsync(ct);
        await cleanup.Subscriptions
            .Where(s => s.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.Amount, originalAmount), ct);
        Ui.Muted($"Початкову суму {originalAmount:N2} відновлено (ExecuteUpdate).");
    }

    private static async Task ResolveAsync(DbUpdateConcurrencyException ex, CancellationToken ct)
    {
        var entry = ex.Entries.Single();
        var databaseValues = await entry.GetDatabaseValuesAsync(ct);
        if (databaseValues is null)
        {
            Ui.Error("Запис уже видалено іншим користувачем — зберігати нікуди.");
            return;
        }

        var currency = (string)entry.CurrentValues[nameof(Subscription.Currency)]!;
        Ui.Table(["Значення", "Сума"],
        [
            ["Б прочитав (Original)", Ui.Money((decimal)entry.OriginalValues[nameof(Subscription.Amount)]!, currency)],
            ["Б хоче зберегти (Current)", Ui.Money((decimal)entry.CurrentValues[nameof(Subscription.Amount)]!, currency)],
            ["Зараз у БД (Database)", Ui.Money((decimal)databaseValues[nameof(Subscription.Amount)]!, currency)]
        ]);

        Ui.Info("Як вчинити?");
        Ui.Info("  1 — перезаписати своїми даними (Client wins)");
        Ui.Info("  2 — скасувати свої зміни і взяти дані з БД (Database wins)");
        var choice = Input.ReadInt("Вибір", 1, 2, 2);

        var context = entry.Context;
        if (choice == 1)
        {
            // Оновлюємо OriginalValues (зокрема RowVersion) до актуальних — повторний UPDATE пройде.
            entry.OriginalValues.SetValues(databaseValues);
            await context.SaveChangesAsync(ct);
            Ui.Success($"Client wins: збережено {Ui.Money((decimal)entry.CurrentValues[nameof(Subscription.Amount)]!, currency)}");
        }
        else
        {
            // Відкидаємо свої зміни: поточні й оригінальні значення = значення з БД.
            entry.CurrentValues.SetValues(databaseValues);
            entry.OriginalValues.SetValues(databaseValues);
            entry.State = EntityState.Unchanged;
            Ui.Success($"Database wins: у Б тепер актуальна сума {Ui.Money((decimal)entry.CurrentValues[nameof(Subscription.Amount)]!, currency)}");
        }
    }
}
