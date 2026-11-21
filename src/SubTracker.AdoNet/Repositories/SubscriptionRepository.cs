using System.Data;
using Microsoft.Data.SqlClient;
using SubTracker.AdoNet.Models;
using SubTracker.Domain;
using SubTracker.Domain.Enums;

namespace SubTracker.AdoNet.Repositories;

/// <summary>
/// ADO.NET CRUD для таблиці Subscriptions (пов'язана з Categories через FK CategoryId).
/// UPDATE реалізує оптимістичну конкуренцію вручну: WHERE ... AND RowVersion = @RowVersion.
/// </summary>
public sealed class SubscriptionRepository(SqlConnectionFactory connectionFactory)
{
    private const string SelectColumns = """
        SELECT s.Id, s.OwnerId, s.CategoryId, c.Name AS CategoryName, s.PaymentMethodId, s.Name,
               s.Amount, s.Currency, s.Period, s.NextChargeDate, s.EndDate, s.Status,
               s.RemindDaysBefore, s.RowVersion
        FROM dbo.Subscriptions AS s
        INNER JOIN dbo.Categories AS c ON c.Id = s.CategoryId
        """;

    /// <summary>READ: підписки власника.</summary>
    public async Task<List<SubscriptionRow>> GetByOwnerAsync(int ownerId, CancellationToken cancellationToken)
    {
        var sql = SelectColumns + "\nWHERE s.OwnerId = @OwnerId\nORDER BY s.NextChargeDate, s.Name;";

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@OwnerId", SqlDbType.Int).Value = ownerId;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<SubscriptionRow>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(Map(reader));
        return result;
    }

    /// <summary>READ: одна підписка або null.</summary>
    public async Task<SubscriptionRow?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var sql = SelectColumns + "\nWHERE s.Id = @Id;";

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    /// <summary>CREATE: вставка з поверненням згенерованого Id.</summary>
    public async Task<int> CreateAsync(NewSubscription s, CancellationToken cancellationToken)
    {
        Validate(s);

        const string sql = """
            INSERT INTO dbo.Subscriptions
                (OwnerId, CategoryId, PaymentMethodId, Name, Amount, Currency, Period,
                 NextChargeDate, EndDate, Status, RemindDaysBefore)
            OUTPUT INSERTED.Id
            VALUES
                (@OwnerId, @CategoryId, @PaymentMethodId, @Name, @Amount, @Currency, @Period,
                 @NextChargeDate, @EndDate, @Status, @RemindDaysBefore);
            """;

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        var p = command.Parameters;
        p.Add("@OwnerId", SqlDbType.Int).Value = s.OwnerId;
        p.Add("@CategoryId", SqlDbType.Int).Value = s.CategoryId;
        p.Add("@PaymentMethodId", SqlDbType.Int).Value = s.PaymentMethodId;
        p.Add("@Name", SqlDbType.NVarChar, 100).Value = s.Name.Trim();
        AddMoney(p, "@Amount", s.Amount);
        p.Add("@Currency", SqlDbType.Char, 3).Value = s.Currency;
        p.Add("@Period", SqlDbType.VarChar, 20).Value = s.Period.ToString();
        p.AddDate("@NextChargeDate", s.NextChargeDate);
        p.AddDate("@EndDate", s.EndDate);
        p.Add("@Status", SqlDbType.VarChar, 20).Value = nameof(SubscriptionStatus.Active);
        p.Add("@RemindDaysBefore", SqlDbType.Int).Value = s.RemindDaysBefore;

        try
        {
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new BusinessRuleException($"У власника вже є підписка «{s.Name}».", ex);
        }
        catch (SqlException ex) when (ex.Number == 547)
        {
            throw new BusinessRuleException("Невірний власник, категорія або спосіб оплати (порушення FK/CHECK).", ex);
        }
    }

    /// <summary>
    /// UPDATE з оптимістичною конкуренцією. Повертає false, якщо рядок змінив хтось інший
    /// (RowVersion вже не збігається) або його видалено.
    /// </summary>
    public async Task<bool> UpdateAsync(
        int id, decimal amount, SubscriptionStatus status, DateOnly nextChargeDate, byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        if (amount <= 0)
            throw new BusinessRuleException("Сума має бути більшою за нуль.");

        const string sql = """
            UPDATE dbo.Subscriptions
            SET Amount = @Amount, Status = @Status, NextChargeDate = @NextChargeDate
            WHERE Id = @Id AND RowVersion = @RowVersion;
            """;

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        AddMoney(command.Parameters, "@Amount", amount);
        command.Parameters.Add("@Status", SqlDbType.VarChar, 20).Value = status.ToString();
        command.Parameters.AddDate("@NextChargeDate", nextChargeDate);
        command.Parameters.Add("@RowVersion", SqlDbType.Timestamp).Value = rowVersion;

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    /// <summary>DELETE: платежі та учасники видаляються каскадом (ON DELETE CASCADE).</summary>
    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM dbo.Subscriptions WHERE Id = @Id;";

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static SubscriptionRow Map(SqlDataReader r) => new(
        Id: r.GetInt32(r.GetOrdinal("Id")),
        OwnerId: r.GetInt32(r.GetOrdinal("OwnerId")),
        CategoryId: r.GetInt32(r.GetOrdinal("CategoryId")),
        CategoryName: r.GetString(r.GetOrdinal("CategoryName")),
        PaymentMethodId: r.GetInt32(r.GetOrdinal("PaymentMethodId")),
        Name: r.GetString(r.GetOrdinal("Name")),
        Amount: r.GetDecimal(r.GetOrdinal("Amount")),
        Currency: r.GetString(r.GetOrdinal("Currency")),
        Period: r.GetEnum<BillingPeriod>("Period"),
        NextChargeDate: r.GetDateOnly("NextChargeDate"),
        EndDate: r.GetNullableDateOnly("EndDate"),
        Status: r.GetEnum<SubscriptionStatus>("Status"),
        RemindDaysBefore: r.GetInt32(r.GetOrdinal("RemindDaysBefore")),
        RowVersion: (byte[])r["RowVersion"]);

    private static void AddMoney(SqlParameterCollection parameters, string name, decimal value)
    {
        var parameter = parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 18;
        parameter.Scale = 2;
        parameter.Value = value;
    }

    private static void Validate(NewSubscription s)
    {
        if (string.IsNullOrWhiteSpace(s.Name) || s.Name.Trim().Length > 100)
            throw new BusinessRuleException("Назва підписки обов'язкова і не довша 100 символів.");
        if (s.Amount <= 0)
            throw new BusinessRuleException("Сума має бути більшою за нуль.");
        if (s.Currency is not { Length: 3 } || !s.Currency.All(char.IsAsciiLetterUpper))
            throw new BusinessRuleException("Валюта — трилітерний код ISO, напр. UAH.");
        if (s.RemindDaysBefore is < 0 or > 30)
            throw new BusinessRuleException("Нагадування — від 0 до 30 днів.");
    }
}
