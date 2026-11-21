using System.Globalization;

namespace SubTracker.ConsoleApp.Infrastructure;

/// <summary>
/// Контроль правильності введення: кожен метод повторює запит, доки користувач
/// не введе коректне значення. Порожній ввід (Enter) приймає значення за замовчуванням.
/// </summary>
public static class Input
{
    public static int ReadInt(string prompt, int min, int max, int? defaultValue = null)
    {
        while (true)
        {
            var text = Ask(prompt, defaultValue?.ToString(CultureInfo.InvariantCulture));
            if (text.Length == 0 && defaultValue is { } d)
                return d;

            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                Ui.Error("Потрібне ціле число.");
            else if (value < min || value > max)
                Ui.Error($"Число має бути в діапазоні {min}..{max}.");
            else
                return value;
        }
    }

    public static decimal ReadDecimal(string prompt, decimal min, decimal max, decimal? defaultValue = null)
    {
        while (true)
        {
            var text = Ask(prompt, defaultValue?.ToString(CultureInfo.InvariantCulture));
            if (text.Length == 0 && defaultValue is { } d)
                return d;

            // Приймаємо і крапку, і кому як десятковий роздільник.
            var normalized = text.Replace(',', '.').Replace(" ", string.Empty);
            if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                Ui.Error("Потрібне число, напр. 199.99");
            else if (value < min || value > max)
                Ui.Error($"Значення має бути в діапазоні {min}..{max}.");
            else if (decimal.Round(value, 2) != value)
                Ui.Error("Не більше двох знаків після коми.");
            else
                return value;
        }
    }

    public static string ReadString(string prompt, int maxLength, string? defaultValue = null)
    {
        while (true)
        {
            var text = Ask(prompt, defaultValue);
            if (text.Length == 0 && defaultValue is not null)
                return defaultValue;

            if (text.Length == 0)
                Ui.Error("Значення не може бути порожнім.");
            else if (text.Length > maxLength)
                Ui.Error($"Не довше {maxLength} символів (зараз {text.Length}).");
            else
                return text;
        }
    }

    /// <summary>Необов'язковий рядок: Enter → null.</summary>
    public static string? ReadOptional(string prompt, int maxLength)
    {
        while (true)
        {
            var text = Ask(prompt + " (Enter — пропустити)", null);
            if (text.Length == 0)
                return null;
            if (text.Length <= maxLength)
                return text;
            Ui.Error($"Не довше {maxLength} символів.");
        }
    }

    public static DateOnly ReadDate(string prompt, DateOnly? defaultValue = null)
    {
        while (true)
        {
            var text = Ask(prompt + " [дд.мм.рррр]", defaultValue?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
            if (text.Length == 0 && defaultValue is { } d)
                return d;

            if (DateOnly.TryParseExact(text, ["dd.MM.yyyy", "d.M.yyyy"], CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var value))
                return value;

            Ui.Error("Невірний формат дати. Приклад: 25.12.2026");
        }
    }

    public static bool Confirm(string prompt, bool defaultValue)
    {
        while (true)
        {
            var text = Ask(prompt + (defaultValue ? " (Т/н)" : " (т/Н)"), null).ToLowerInvariant();
            switch (text)
            {
                case "": return defaultValue;
                case "т" or "так" or "y" or "yes": return true;
                case "н" or "ні" or "n" or "no": return false;
                default: Ui.Error("Введіть «т» (так) або «н» (ні)."); break;
            }
        }
    }

    public static void Pause()
    {
        Ui.Muted("Натисніть Enter, щоб продовжити…");
        Console.ReadLine();
    }

    private static string Ask(string prompt, string? defaultValue)
    {
        Console.Write(defaultValue is null ? $"{prompt}: " : $"{prompt} [{defaultValue}]: ");
        var line = Console.ReadLine() ?? throw new OperationCanceledException("Ввід завершено.");
        return line.Trim();
    }
}
