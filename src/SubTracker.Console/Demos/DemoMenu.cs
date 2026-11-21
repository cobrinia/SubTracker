using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SubTracker.ConsoleApp.Infrastructure;
using SubTracker.DB;
using SubTracker.Domain;

namespace SubTracker.ConsoleApp.Demos;

/// <summary>Головне меню демонстрації (пункт 14 ходу роботи).</summary>
public sealed class DemoMenu(
    IDbContextFactory<SubTrackerDbContext> contextFactory,
    StateDemo state,
    AdoNetDemo ado,
    EfCoreDemo ef,
    LinqDemo linq,
    ConcurrencyDemo concurrency,
    SqlDiagnosticsDemo sql)
{
    private CancellationTokenSource? _current;

    private IReadOnlyList<(string Key, string Title, Func<CancellationToken, Task> Action)> Items =>
    [
        ("1", "Початковий стан даних", state.ShowInitialStateAsync),
        ("2", "ADO.NET: CRUD Categories", ado.CategoriesCrudAsync),
        ("3", "ADO.NET: CRUD Subscriptions", ado.SubscriptionsCrudAsync),
        ("4", "ADO.NET: складні запити (JOIN 4 таблиць, GROUP BY)", ado.ComplexQueriesAsync),
        ("5", "ADO.NET: транзакція — COMMIT", ado.TransactionCommitAsync),
        ("6", "ADO.NET: транзакція — ROLLBACK", ado.TransactionRollbackAsync),
        ("7", "EF Core: CRUD User / PaymentMethod / Subscription", ef.CrudAsync),
        ("8", "EF Core: бізнес-операція «сімейний тариф» (транзакція)", ef.FamilyUpgradeAsync),
        ("9", "EF Core: LINQ-запити", linq.RunAllAsync),
        ("10", "EF Core: пагінація", linq.PaginationAsync),
        ("11", "Optimistic concurrency (два DbContext)", concurrency.RunAsync),
        ("12", "SQL від EF Core: ToQueryString, логування, N+1", sql.RunAsync),
        ("13", "Міграції", state.ShowMigrationsAsync)
    ];

    public async Task RunAsync()
    {
        // Ctrl+C скасовує поточну операцію (через CancellationToken), а не закриває програму.
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _current?.Cancel();
        };

        if (!await CheckDatabaseAsync())
            return;

        while (true)
        {
            Ui.Title("МЕНЕДЖЕР ПІДПИСОК — демонстрація ADO.NET та EF Core");
            foreach (var (key, title, _) in Items)
                Ui.Info($"  {key,2}. {title}");
            Ui.Info("   0. Вихід");

            Console.Write("Ваш вибір: ");
            var choice = Console.ReadLine()?.Trim();
            if (choice is null or "0")
                return;

            var item = Items.FirstOrDefault(i => i.Key == choice);
            if (item.Action is null)
            {
                Ui.Error("Немає такого пункту меню.");
                continue;
            }

            await ExecuteAsync(item.Action);
            Input.Pause();
        }
    }

    /// <summary>Виконує пункт меню з єдиною обробкою помилок.</summary>
    private async Task ExecuteAsync(Func<CancellationToken, Task> action)
    {
        _current = new CancellationTokenSource();
        try
        {
            await action(_current.Token);
        }
        catch (BusinessRuleException ex)
        {
            Ui.Error(ex.Message);
        }
        catch (OperationCanceledException)
        {
            Ui.Warning("Операцію скасовано.");
        }
        catch (SqlException ex)
        {
            Ui.Error($"Помилка SQL Server №{ex.Number}: {ex.Message}");
        }
        catch (DbUpdateException ex)
        {
            Ui.Error($"Помилка збереження: {ex.InnerException?.Message ?? ex.Message}");
        }
        catch (Exception ex)
        {
            Ui.Error($"{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _current.Dispose();
            _current = null;
        }
    }

    /// <summary>Перевіряє з'єднання і міграції перед показом меню.</summary>
    private async Task<bool> CheckDatabaseAsync()
    {
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync();

            if (!db.Database.GetMigrations().Any())
            {
                Ui.Error("У проєкті ще немає міграцій. Створіть першу:");
                Ui.Info("  dotnet ef migrations add InitialCreate --project src/SubTracker.DB --startup-project src/SubTracker.Console");
                return false;
            }

            var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
            if (pending.Count > 0)
            {
                Ui.Warning("Є незастосовані міграції: " + string.Join(", ", pending));
                Ui.Muted("Рекомендовано: dotnet ef database update --project src/SubTracker.DB --startup-project src/SubTracker.Console");
                if (Input.Confirm("Застосувати їх зараз з коду (Database.MigrateAsync, з UseAsyncSeeding)?", false))
                {
                    await db.Database.MigrateAsync();
                    Ui.Success("Міграції застосовано, тестові дані додано.");
                }
                else
                {
                    return false;
                }
            }

            return true;
        }
        catch (SqlException ex)
        {
            Ui.Error($"Не вдалося підключитися до SQL Server: {ex.Message}");
            Ui.Muted("Перевірте рядок підключення в user secrets і що LocalDB запущено: sqllocaldb info MSSQLLocalDB");
            return false;
        }
    }
}
