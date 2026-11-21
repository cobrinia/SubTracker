using System.Data;
using Microsoft.Data.SqlClient;
using SubTracker.AdoNet.Models;
using SubTracker.Domain;

namespace SubTracker.AdoNet.Repositories;

/// <summary>
/// ADO.NET CRUD для таблиці Categories.
/// Усі значення передаються лише через SqlParameter з явним типом —
/// жодної конкатенації рядків, тому SQL-ін'єкція неможлива.
/// </summary>
public sealed class CategoryRepository(SqlConnectionFactory connectionFactory)
{
    /// <summary>READ: усі категорії з кількістю підписок (LEFT JOIN + GROUP BY).</summary>
    public async Task<List<CategoryRow>> GetAllAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT c.Id, c.Name, COUNT(s.Id) AS SubscriptionCount
            FROM dbo.Categories AS c
            LEFT JOIN dbo.Subscriptions AS s ON s.CategoryId = c.Id
            GROUP BY c.Id, c.Name
            ORDER BY c.Name;
            """;

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var result = new List<CategoryRow>();
        // Ordinal-и обчислюємо один раз, а не на кожному рядку.
        int idOrd = reader.GetOrdinal("Id"), nameOrd = reader.GetOrdinal("Name"), countOrd = reader.GetOrdinal("SubscriptionCount");
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new CategoryRow(reader.GetInt32(idOrd), reader.GetString(nameOrd), reader.GetInt32(countOrd)));
        }
        return result;
    }

    /// <summary>READ: одна категорія або null.</summary>
    public async Task<CategoryRow?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT c.Id, c.Name,
                   (SELECT COUNT(*) FROM dbo.Subscriptions AS s WHERE s.CategoryId = c.Id) AS SubscriptionCount
            FROM dbo.Categories AS c
            WHERE c.Id = @Id;
            """;

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new CategoryRow(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2));
    }

    /// <summary>CREATE: повертає Id нового рядка через OUTPUT INSERTED.Id і ExecuteScalar.</summary>
    public async Task<int> CreateAsync(string name, CancellationToken cancellationToken)
    {
        ValidateName(name);

        const string sql = """
            INSERT INTO dbo.Categories (Name)
            OUTPUT INSERTED.Id
            VALUES (@Name);
            """;

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Name", SqlDbType.NVarChar, 50).Value = name.Trim();

        try
        {
            var id = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(id);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new BusinessRuleException($"Категорія «{name.Trim()}» вже існує.", ex);
        }
    }

    /// <summary>UPDATE: true — якщо рядок знайдено й змінено.</summary>
    public async Task<bool> UpdateAsync(int id, string name, CancellationToken cancellationToken)
    {
        ValidateName(name);

        const string sql = "UPDATE dbo.Categories SET Name = @Name WHERE Id = @Id;";

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        command.Parameters.Add("@Name", SqlDbType.NVarChar, 50).Value = name.Trim();

        try
        {
            return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new BusinessRuleException($"Категорія «{name.Trim()}» вже існує.", ex);
        }
    }

    /// <summary>
    /// DELETE: FK Subscriptions.CategoryId налаштований як Restrict (NO ACTION),
    /// тому категорію з підписками СКБД видалити не дасть — помилка 547.
    /// </summary>
    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM dbo.Categories WHERE Id = @Id;";

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

        try
        {
            return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        }
        catch (SqlException ex) when (ex.Number == 547)
        {
            throw new BusinessRuleException("Категорію не можна видалити: до неї прив'язані підписки (FK RESTRICT).", ex);
        }
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessRuleException("Назва категорії не може бути порожньою.");
        if (name.Trim().Length > 50)
            throw new BusinessRuleException("Назва категорії — не довше 50 символів.");
    }
}
