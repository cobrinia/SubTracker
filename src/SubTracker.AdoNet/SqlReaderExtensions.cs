using System.Data;
using Microsoft.Data.SqlClient;

namespace SubTracker.AdoNet;

/// <summary>
/// Допоміжні методи читання/передачі значень. DateOnly передається як SqlDbType.Date,
/// а читається з DateTime — це працює з будь-якою версією SqlClient.
/// </summary>
internal static class SqlReaderExtensions
{
    public static DateOnly GetDateOnly(this SqlDataReader reader, string column) =>
        DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal(column)));

    public static DateOnly? GetNullableDateOnly(this SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : DateOnly.FromDateTime(reader.GetDateTime(ordinal));
    }

    public static DateTime? GetNullableDateTime(this SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    public static TEnum GetEnum<TEnum>(this SqlDataReader reader, string column) where TEnum : struct, Enum =>
        Enum.Parse<TEnum>(reader.GetString(reader.GetOrdinal(column)));

    /// <summary>Додає параметр типу date (NULL, якщо значення немає).</summary>
    public static void AddDate(this SqlParameterCollection parameters, string name, DateOnly? value) =>
        parameters.Add(name, SqlDbType.Date).Value =
            value is { } d ? d.ToDateTime(TimeOnly.MinValue) : (object)DBNull.Value;
}
