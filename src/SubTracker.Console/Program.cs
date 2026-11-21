using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SubTracker.AdoNet;
using SubTracker.AdoNet.Reports;
using SubTracker.AdoNet.Repositories;
using SubTracker.AdoNet.Transactions;
using SubTracker.ConsoleApp.Demos;
using SubTracker.DB;
using SubTracker.DB.Queries;
using SubTracker.DB.Services;

// ---------- Консоль: UTF-8 для кирилиці, українські формати чисел і дат ----------
Console.OutputEncoding = Encoding.UTF8;
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("uk-UA");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("uk-UA");

// ---------- Конфігурація: appsettings.json → user secrets → змінні середовища → аргументи ----------
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory // appsettings.json шукаємо поруч з .exe
});

// Консольний застосунок за замовчуванням працює в середовищі Production, де user secrets
// автоматично не підключаються, тому додаємо їх явно.
builder.Configuration.AddUserSecrets<Program>(optional: true);

var connectionString = builder.Configuration.GetConnectionString("SubTrackerDb")
    ?? throw new InvalidOperationException(
        "Рядок підключення 'ConnectionStrings:SubTrackerDb' не знайдено. " +
        "Додайте його в user secrets (правий клік по SubTracker.Console → Manage User Secrets).");

var sensitiveLogging = builder.Configuration.GetValue<bool>("Demo:EnableSensitiveDataLogging");

// ---------- Впровадження залежностей ----------

// EF Core: фабрика контекстів — кожна операція створює власний короткоживучий DbContext,
// а демонстрація конкуренції — два незалежні контексти.
builder.Services.AddDbContextFactory<SubTrackerDbContext>(options =>
    options.UseSubTracker(connectionString, sensitiveLogging));

builder.Services.AddSingleton<UserService>();
builder.Services.AddSingleton<PaymentMethodService>();
builder.Services.AddSingleton<SubscriptionService>();
builder.Services.AddSingleton<FamilyPlanService>();
builder.Services.AddSingleton<SubscriptionQueries>();

// ADO.NET
builder.Services.AddSingleton(new SqlConnectionFactory(connectionString));
builder.Services.AddSingleton<CategoryRepository>();
builder.Services.AddSingleton<SubscriptionRepository>();
builder.Services.AddSingleton<ReportQueries>();
builder.Services.AddSingleton<ChargeTransactionService>();

// Демонстрація
builder.Services.AddSingleton<StateDemo>();
builder.Services.AddSingleton<AdoNetDemo>();
builder.Services.AddSingleton<EfCoreDemo>();
builder.Services.AddSingleton<LinqDemo>();
builder.Services.AddSingleton<ConcurrencyDemo>();
builder.Services.AddSingleton<SqlDiagnosticsDemo>();
builder.Services.AddSingleton<DemoMenu>();

using var host = builder.Build();

await host.Services.GetRequiredService<DemoMenu>().RunAsync();
