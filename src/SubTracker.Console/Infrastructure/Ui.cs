using System.Globalization;

namespace SubTracker.ConsoleApp.Infrastructure;

/// <summary>Оформлення виводу в консоль: заголовки, кольорові повідомлення, таблиці.</summary>
public static class Ui
{
    public static void Title(string text)
    {
        Console.WriteLine();
        Write(ConsoleColor.Yellow, new string('═', Math.Min(Math.Max(text.Length + 4, 40), 100)));
        Write(ConsoleColor.Yellow, $"  {text}");
        Write(ConsoleColor.Yellow, new string('═', Math.Min(Math.Max(text.Length + 4, 40), 100)));
    }

    public static void Section(string text)
    {
        Console.WriteLine();
        Write(ConsoleColor.Cyan, $"── {text} ──");
    }

    public static void Info(string text) => Console.WriteLine(text);
    public static void Muted(string text) => Write(ConsoleColor.DarkGray, text);
    public static void Success(string text) => Write(ConsoleColor.Green, $"✔ {text}");
    public static void Warning(string text) => Write(ConsoleColor.DarkYellow, $"⚠ {text}");
    public static void Error(string text) => Write(ConsoleColor.Red, $"✘ {text}");

    public static void Write(ConsoleColor color, string text)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ForegroundColor = previous;
    }

    public static string Money(decimal amount, string currency) =>
        $"{amount.ToString("N2", CultureInfo.CurrentCulture)} {currency}";

    public static string Date(DateOnly date) => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
    public static string Date(DateOnly? date) => date is { } d ? Date(d) : "—";
    public static string DateTimeText(DateTime? value) =>
        value is { } v ? v.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture) : "—";

    public static string Hex(byte[]? bytes) => bytes is null ? "—" : "0x" + Convert.ToHexString(bytes);

    /// <summary>Друкує таблицю з автоматичною шириною стовпців.</summary>
    public static void Table(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        const int maxWidth = 32;
        var data = rows.Select(r => r.Select(c => Truncate(c ?? string.Empty, maxWidth)).ToArray()).ToList();

        var widths = headers.Select(h => h.Length).ToArray();
        foreach (var row in data)
            for (var i = 0; i < widths.Length && i < row.Length; i++)
                widths[i] = Math.Max(widths[i], row[i].Length);

        string Line(IReadOnlyList<string> cells) =>
            "│ " + string.Join(" │ ", widths.Select((w, i) => (i < cells.Count ? cells[i] : string.Empty).PadRight(w))) + " │";
        var separator = "├─" + string.Join("─┼─", widths.Select(w => new string('─', w))) + "─┤";

        Write(ConsoleColor.White, "┌─" + string.Join("─┬─", widths.Select(w => new string('─', w))) + "─┐");
        Write(ConsoleColor.White, Line(headers));
        Console.WriteLine(separator);
        foreach (var row in data)
            Console.WriteLine(Line(row));
        Console.WriteLine("└─" + string.Join("─┴─", widths.Select(w => new string('─', w))) + "─┘");

        if (data.Count == 0)
            Muted("(немає рядків)");
        else
            Muted($"Рядків: {data.Count}");
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";
}
