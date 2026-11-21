namespace SubTracker.DB.Diagnostics;

/// <summary>
/// Простий приймач логів EF Core (LogTo). За замовчуванням вимкнений,
/// щоб SQL не засмічував вивід; демонстрація вмикає його тимчасово.
/// </summary>
public static class SqlLog
{
    private static volatile bool _enabled;

    public static bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <summary>Кількість SQL-команд, виконаних поки логування було ввімкнене (для перевірки N+1).</summary>
    public static int CommandCount { get; private set; }

    public static void Write(string message)
    {
        if (!_enabled)
            return;

        CommandCount++;
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine(message);
        Console.ForegroundColor = previous;
    }

    /// <summary>Вмикає лог до виклику Dispose() результату.</summary>
    public static IDisposable Capture()
    {
        CommandCount = 0;
        Enabled = true;
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        public void Dispose() => Enabled = false;
    }
}
