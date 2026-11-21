using System.Data;
using Microsoft.Data.SqlClient;
using SubTracker.AdoNet.Models;
using SubTracker.Domain;
using SubTracker.Domain.Billing;
using SubTracker.Domain.Enums;

namespace SubTracker.AdoNet.Transactions;

/// <summary>
/// ADO.NET-транзакція «Провести списання за підпискою», яка змінює ТРИ таблиці:
///   1) INSERT  Payments        — запис про платіж;
///   2) UPDATE  Subscriptions   — зсув дати наступного списання (і статус, якщо підписка завершилась);
///   3) UPDATE  PaymentMethods  — дата останнього використання картки;
///   4) «платіжний шлюз» перевіряє картку: прострочена → BusinessRuleException → ROLLBACK.
/// Або виконуються всі зміни (COMMIT), або жодна (ROLLBACK) — атомарність ACID.
/// </summary>
public sealed class ChargeTransactionService(SqlConnectionFactory connectionFactory)
{
    public async Task<ChargeResult> ProcessChargeAsync(int subscriptionId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        // Усі команди нижче отримують цю транзакцію (третій аргумент SqlCommand).
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            // ---- 0. Читання з блокуванням рядка (UPDLOCK): паралельне списання тієї ж підписки чекатиме ----
            const string selectSql = """
                SELECT s.Name, s.Amount, s.Currency, s.Period, s.NextChargeDate, s.EndDate, s.Status,
                       s.PaymentMethodId, pm.Title AS CardTitle, pm.ExpiryDate
                FROM dbo.Subscriptions AS s WITH (UPDLOCK, ROWLOCK)
                INNER JOIN dbo.PaymentMethods AS pm ON pm.Id = s.PaymentMethodId
                WHERE s.Id = @Id;
                """;

            string name, currency, cardTitle;
            decimal amount;
            BillingPeriod period;
            DateOnly nextChargeDate;
            DateOnly? endDate, cardExpiry;
            SubscriptionStatus status;
            int paymentMethodId;

            await using (var select = new SqlCommand(selectSql, connection, transaction))
            {
                select.Parameters.Add("@Id", SqlDbType.Int).Value = subscriptionId;
                await using var reader = await select.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                    throw new BusinessRuleException($"Підписку Id={subscriptionId} не знайдено.");

                name = reader.GetString(reader.GetOrdinal("Name"));
                amount = reader.GetDecimal(reader.GetOrdinal("Amount"));
                currency = reader.GetString(reader.GetOrdinal("Currency"));
                period = reader.GetEnum<BillingPeriod>("Period");
                nextChargeDate = reader.GetDateOnly("NextChargeDate");
                endDate = reader.GetNullableDateOnly("EndDate");
                status = reader.GetEnum<SubscriptionStatus>("Status");
                paymentMethodId = reader.GetInt32(reader.GetOrdinal("PaymentMethodId"));
                cardTitle = reader.GetString(reader.GetOrdinal("CardTitle"));
                cardExpiry = reader.GetNullableDateOnly("ExpiryDate");
            } // reader закрито — без MARS на одному з'єднанні не можна виконувати іншу команду при відкритому reader

            if (status != SubscriptionStatus.Active)
                throw new BusinessRuleException($"Підписка «{name}» має статус {status} — списання заборонене.");

            // ---- 1. INSERT Payments ----
            const string insertPaymentSql = """
                INSERT INTO dbo.Payments (SubscriptionId, PaymentMethodId, ChargedAt, Amount, Currency, Status)
                OUTPUT INSERTED.Id
                VALUES (@SubscriptionId, @PaymentMethodId, SYSUTCDATETIME(), @Amount, @Currency, @Status);
                """;

            int paymentId;
            await using (var insert = new SqlCommand(insertPaymentSql, connection, transaction))
            {
                insert.Parameters.Add("@SubscriptionId", SqlDbType.Int).Value = subscriptionId;
                insert.Parameters.Add("@PaymentMethodId", SqlDbType.Int).Value = paymentMethodId;
                var amountParam = insert.Parameters.Add("@Amount", SqlDbType.Decimal);
                amountParam.Precision = 18;
                amountParam.Scale = 2;
                amountParam.Value = amount;
                insert.Parameters.Add("@Currency", SqlDbType.Char, 3).Value = currency;
                insert.Parameters.Add("@Status", SqlDbType.VarChar, 20).Value = nameof(PaymentStatus.Succeeded);
                paymentId = Convert.ToInt32(await insert.ExecuteScalarAsync(cancellationToken));
            }

            // ---- 2. UPDATE Subscriptions ----
            var newNextDate = BillingCalculator.NextDate(nextChargeDate, period);
            var newStatus = endDate is not null && newNextDate > endDate
                ? SubscriptionStatus.Cancelled // строк дії закінчився — далі не продовжуємо
                : SubscriptionStatus.Active;

            const string updateSubscriptionSql = """
                UPDATE dbo.Subscriptions
                SET NextChargeDate = @NextChargeDate, Status = @Status
                WHERE Id = @Id;
                """;

            await using (var update = new SqlCommand(updateSubscriptionSql, connection, transaction))
            {
                update.Parameters.Add("@Id", SqlDbType.Int).Value = subscriptionId;
                update.Parameters.AddDate("@NextChargeDate", newNextDate);
                update.Parameters.Add("@Status", SqlDbType.VarChar, 20).Value = newStatus.ToString();
                await update.ExecuteNonQueryAsync(cancellationToken);
            }

            // ---- 3. UPDATE PaymentMethods ----
            const string updateCardSql = "UPDATE dbo.PaymentMethods SET LastUsedAt = SYSUTCDATETIME() WHERE Id = @Id;";
            await using (var updateCard = new SqlCommand(updateCardSql, connection, transaction))
            {
                updateCard.Parameters.Add("@Id", SqlDbType.Int).Value = paymentMethodId;
                await updateCard.ExecuteNonQueryAsync(cancellationToken);
            }

            // ---- 4. Відповідь «платіжного шлюзу» ----
            // Записи вже зроблені, але ще не зафіксовані. Якщо шлюз відхиляє оплату — відкочуємо всі три зміни.
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (cardExpiry is not null && cardExpiry < today)
                throw new BusinessRuleException(
                    $"Платіжний шлюз відхилив оплату: картка «{cardTitle}» прострочена ({cardExpiry:dd.MM.yyyy}).");

            await transaction.CommitAsync(cancellationToken);

            return new ChargeResult(paymentId, subscriptionId, name, amount, currency, nextChargeDate, newNextDate, newStatus);
        }
        catch
        {
            // CancellationToken.None: відкат має відбутися навіть якщо операцію скасували.
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
                // Транзакцію вже завершив сервер (напр. після критичної помилки) — відкочувати нічого.
            }
            throw;
        }
    }

    /// <summary>
    /// Знімок трьох таблиць, які змінює транзакція. Читається до і після —
    /// так у демонстрації видно, що після Rollback нічого не змінилось.
    /// </summary>
    public async Task<ChargeStateSnapshot> GetStateAsync(int subscriptionId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT (SELECT COUNT(*) FROM dbo.Payments WHERE SubscriptionId = @Id) AS PaymentsCount,
                   s.NextChargeDate, s.Status, pm.LastUsedAt
            FROM dbo.Subscriptions AS s
            INNER JOIN dbo.PaymentMethods AS pm ON pm.Id = s.PaymentMethodId
            WHERE s.Id = @Id;
            """;

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = subscriptionId;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            return new ChargeStateSnapshot(0, null, null, null);

        return new ChargeStateSnapshot(
            reader.GetInt32(0),
            reader.GetDateOnly("NextChargeDate"),
            reader.GetEnum<SubscriptionStatus>("Status"),
            reader.GetNullableDateTime("LastUsedAt"));
    }
}
