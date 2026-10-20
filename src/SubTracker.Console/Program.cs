using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddUserSecrets<Program>();

var connectionString = builder.Configuration.GetConnectionString("SubTrackerDb")
    ?? throw new InvalidOperationException("Connection string 'SubTrackerDb' is missing.");

Console.WriteLine($"Рядок підключення завантажено: {connectionString}");