using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using SubTracker.DB;

namespace SubTracker.ConsoleApp.Infrastructure;

/// <summary>
/// Фабрика контексту для інструмента dotnet ef (migrations add, database update, migrations script).
/// Інструмент знаходить її автоматично і не запускає застосунок. Рядок підключення
/// береться з тих самих user secrets, що й у Program.cs.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SubTrackerDbContext>
{
    public SubTrackerDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(DesignTimeDbContextFactory).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("SubTrackerDb")
            ?? throw new InvalidOperationException(
                "Рядок підключення 'ConnectionStrings:SubTrackerDb' не знайдено в user secrets проєкту SubTracker.Console.");

        var options = new DbContextOptionsBuilder<SubTrackerDbContext>();
        options.UseSubTracker(connectionString);
        return new SubTrackerDbContext(options.Options);
    }
}
