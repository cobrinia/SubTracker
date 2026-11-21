using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace SubTracker.DB.Services;

/// <summary>
/// Розпізнавання типових помилок SQL Server всередині DbUpdateException (розд. 28 методички).
/// Номери помилок: 2601/2627 — порушення унікальності, 547 — порушення FK або CHECK.
/// </summary>
internal static class DbErrors
{
    public static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };

    public static bool IsConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 547 };
}
