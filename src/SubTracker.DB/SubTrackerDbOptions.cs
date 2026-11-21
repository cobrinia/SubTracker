using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using SubTracker.DB.Diagnostics;
using SubTracker.DB.Seeding;

namespace SubTracker.DB;

/// <summary>
/// Єдине місце налаштування DbContextOptions. Використовується і застосунком (DI),
/// і design-time фабрикою для dotnet ef, тому конфігурація завжди однакова.
/// </summary>
public static class SubTrackerDbOptions
{
    public static DbContextOptionsBuilder UseSubTracker(
        this DbContextOptionsBuilder builder,
        string connectionString,
        bool enableSensitiveDataLogging = false)
    {
        builder
            .UseSqlServer(connectionString)
            // Тестові дані для транзакційних таблиць (розд. 30 методички).
            // UseSeeding викликається з Migrate()/EnsureCreated() — зокрема з `dotnet ef database update`,
            // UseAsyncSeeding — з MigrateAsync()/EnsureCreatedAsync().
            .UseSeeding((context, _) => DataSeeder.Seed(context))
            .UseAsyncSeeding((context, _, cancellationToken) => DataSeeder.SeedAsync(context, cancellationToken))
            // Логування SQL, який EF Core надсилає до СКБД. Вмикається/вимикається в демонстрації.
            .LogTo(SqlLog.Write, new[] { RelationalEventId.CommandExecuted }, LogLevel.Information);

        if (enableSensitiveDataLogging)
        {
            // Показує значення параметрів у лозі SQL. Тільки для розробки!
            builder.EnableSensitiveDataLogging();
        }

        return builder;
    }
}
