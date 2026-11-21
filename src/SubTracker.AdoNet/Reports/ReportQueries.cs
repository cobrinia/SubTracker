using System.Data;
using Microsoft.Data.SqlClient;
using SubTracker.AdoNet.Models;
using SubTracker.Domain.Enums;

namespace SubTracker.AdoNet.Reports;

/// <summary>Складні SQL-запити через ADO.NET (пункт 5 ходу роботи).</summary>
public sealed class ReportQueries(SqlConnectionFactory connectionFactory)
{
    /// <summary>
    /// Запит 1 — JOIN чотирьох таблиць з фільтрами та сортуванням:
    /// майбутні списання активних підписок у діапазоні дат, необов'язково — лише в одній валюті.
    /// </summary>
    public async Task<List<UpcomingChargeRow>> GetUpcomingChargesAsync(
        DateOnly from, DateOnly to, string? currency, CancellationToken cancellationToken)
    {
        if (to < from)
            throw new ArgumentException("Кінець періоду не може бути раніше за початок.", nameof(to));

        const string sql = """
            SELECT s.Id, s.Name, s.Amount, s.Currency, s.Period, s.NextChargeDate,
                   u.Name  AS OwnerName,
                   c.Name  AS CategoryName,
                   pm.Title AS PaymentMethodTitle
            FROM dbo.Subscriptions AS s
            INNER JOIN dbo.Users          AS u  ON u.Id  = s.OwnerId
            INNER JOIN dbo.Categories     AS c  ON c.Id  = s.CategoryId
            INNER JOIN dbo.PaymentMethods AS pm ON pm.Id = s.PaymentMethodId
            WHERE s.Status = @Status
              AND s.NextChargeDate BETWEEN @From AND @To
              AND (@Currency IS NULL OR s.Currency = @Currency)
            ORDER BY s.NextChargeDate ASC, s.Amount DESC;
            """;

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Status", SqlDbType.VarChar, 20).Value = nameof(SubscriptionStatus.Active);
        command.Parameters.AddDate("@From", from);
        command.Parameters.AddDate("@To", to);
        command.Parameters.Add("@Currency", SqlDbType.Char, 3).Value =
            string.IsNullOrWhiteSpace(currency) ? DBNull.Value : (object)currency.Trim().ToUpperInvariant();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<UpcomingChargeRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new UpcomingChargeRow(
                reader.GetInt32(reader.GetOrdinal("Id")),
                reader.GetString(reader.GetOrdinal("Name")),
                reader.GetDecimal(reader.GetOrdinal("Amount")),
                reader.GetString(reader.GetOrdinal("Currency")),
                reader.GetEnum<BillingPeriod>("Period"),
                reader.GetDateOnly("NextChargeDate"),
                reader.GetString(reader.GetOrdinal("OwnerName")),
                reader.GetString(reader.GetOrdinal("CategoryName")),
                reader.GetString(reader.GetOrdinal("PaymentMethodTitle"))));
        }
        return result;
    }

    /// <summary>
    /// Запит 2 — агрегований GROUP BY з кількома агрегатними функціями (COUNT, SUM, AVG, MAX, MIN):
    /// витрати на активні підписки за категоріями та валютами з приведенням до місячної суми.
    /// HAVING відсікає групи з кількістю підписок менше заданої.
    /// </summary>
    public async Task<List<CategorySpendingRow>> GetSpendingByCategoryAsync(int minCount, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT c.Name     AS CategoryName,
                   s.Currency AS Currency,
                   COUNT(*)   AS ActiveCount,
                   SUM(CASE s.Period
                           WHEN 'Weekly'    THEN s.Amount * 52 / 12
                           WHEN 'Quarterly' THEN s.Amount / 3
                           WHEN 'Yearly'    THEN s.Amount / 12
                           ELSE s.Amount
                       END)              AS MonthlyTotal,
                   AVG(s.Amount)         AS AverageAmount,
                   MAX(s.Amount)         AS MaxAmount,
                   MIN(s.NextChargeDate) AS NearestCharge
            FROM dbo.Subscriptions AS s
            INNER JOIN dbo.Categories AS c ON c.Id = s.CategoryId
            WHERE s.Status = @Status
            GROUP BY c.Name, s.Currency
            HAVING COUNT(*) >= @MinCount
            ORDER BY MonthlyTotal DESC, c.Name;
            """;

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Status", SqlDbType.VarChar, 20).Value = nameof(SubscriptionStatus.Active);
        command.Parameters.Add("@MinCount", SqlDbType.Int).Value = minCount;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<CategorySpendingRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new CategorySpendingRow(
                reader.GetString(reader.GetOrdinal("CategoryName")),
                reader.GetString(reader.GetOrdinal("Currency")),
                reader.GetInt32(reader.GetOrdinal("ActiveCount")),
                reader.GetDecimal(reader.GetOrdinal("MonthlyTotal")),
                reader.GetDecimal(reader.GetOrdinal("AverageAmount")),
                reader.GetDecimal(reader.GetOrdinal("MaxAmount")),
                reader.GetDateOnly("NearestCharge")));
        }
        return result;
    }

    /// <summary>Активні підписки з картками (для вибору в демонстрації транзакції).</summary>
    public async Task<List<ChargeCandidateRow>> GetChargeCandidatesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT s.Id, s.Name, s.Amount, s.Currency, s.NextChargeDate, pm.Title, pm.ExpiryDate
            FROM dbo.Subscriptions AS s
            INNER JOIN dbo.PaymentMethods AS pm ON pm.Id = s.PaymentMethodId
            WHERE s.Status = @Status
            ORDER BY s.NextChargeDate, s.Id;
            """;

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Status", SqlDbType.VarChar, 20).Value = nameof(SubscriptionStatus.Active);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<ChargeCandidateRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ChargeCandidateRow(
                reader.GetInt32(0), reader.GetString(1), reader.GetDecimal(2), reader.GetString(3),
                reader.GetDateOnly("NextChargeDate"), reader.GetString(5), reader.GetNullableDateOnly("ExpiryDate")));
        }
        return result;
    }

    /// <summary>Кількість рядків у всіх таблицях одним запитом (стан БД).</summary>
    public async Task<Dictionary<string, int>> GetTableCountsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT 'Users' AS TableName, COUNT(*) AS Cnt FROM dbo.Users
            UNION ALL SELECT 'Categories', COUNT(*) FROM dbo.Categories
            UNION ALL SELECT 'PaymentMethods', COUNT(*) FROM dbo.PaymentMethods
            UNION ALL SELECT 'Subscriptions', COUNT(*) FROM dbo.Subscriptions
            UNION ALL SELECT 'SubscriptionMembers', COUNT(*) FROM dbo.SubscriptionMembers
            UNION ALL SELECT 'Payments', COUNT(*) FROM dbo.Payments;
            """;

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var result = new Dictionary<string, int>();
        while (await reader.ReadAsync(cancellationToken))
            result[reader.GetString(0)] = reader.GetInt32(1);
        return result;
    }
}
