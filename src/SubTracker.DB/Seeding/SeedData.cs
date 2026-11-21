using SubTracker.Domain.Entities;

namespace SubTracker.DB.Seeding;

/// <summary>
/// Статичні дані для HasData: довідник категорій і головна таблиця користувачів.
/// HasData вимагає сталих значень (жодного DateTime.Now) і явних первинних ключів,
/// бо ці дані стають частиною міграції.
/// </summary>
public static class SeedData
{
    public static Category[] Categories() =>
    [
        new() { Id = 1, Name = "Стрімінг відео" },
        new() { Id = 2, Name = "Музика" },
        new() { Id = 3, Name = "Програмне забезпечення" },
        new() { Id = 4, Name = "Хмарні сховища" },
        new() { Id = 5, Name = "Зв'язок та інтернет" },
        new() { Id = 6, Name = "Освіта" },
        new() { Id = 7, Name = "Спорт і здоров'я" },
        new() { Id = 8, Name = "Побутові послуги" }
    ];

    public static User[] Users() =>
    [
        new() { Id = 1, Name = "Олександр Коваленко", Email = "o.kovalenko@example.com", CreatedAt = Utc(2026, 1, 12) },
        new() { Id = 2, Name = "Марія Коваленко",     Email = "m.kovalenko@example.com", CreatedAt = Utc(2026, 1, 12) },
        new() { Id = 3, Name = "Андрій Шевченко",     Email = "a.shevchenko@example.com", CreatedAt = Utc(2026, 2, 3) },
        new() { Id = 4, Name = "Ірина Бондар",        Email = "i.bondar@example.com",    CreatedAt = Utc(2026, 3, 18) },
        new() { Id = 5, Name = "Тарас Мельник",       Email = "t.melnyk@example.com",    CreatedAt = Utc(2026, 4, 7) }
    ];

    private static DateTime Utc(int year, int month, int day) =>
        new(year, month, day, 9, 0, 0, DateTimeKind.Utc);
}
