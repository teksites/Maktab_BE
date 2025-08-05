using Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection.PortableExecutable;
namespace Cumulus.Data
{
    public static class Extensions
    {
        public static IDbCommand AddParameter(this IDbCommand cmd, string name, object? value)
        {
            var parameter = cmd.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            cmd.Parameters.Add(parameter);
            return cmd;
        }
        public static void AddArrayParameters<T>(this IDbCommand cmd, string name, IEnumerable<T> values)
        {
            name = name.StartsWith("@") ? name : "@" + name;
            var names =
            string.Join(", ", values.Select((value, i) =>
            { var paramName = name + 1;
                cmd.AddParameter(paramName, value);
                return paramName;
            }));

            cmd.CommandText = cmd.CommandText.Replace(name, names);
        }
        public static void AddDictionaryListParameters<TK, TV>(this IDbCommand cmd, string name, IEnumerable<IDictionary<TK, TV>> values)
        {
            var documentsAndReturnsClause = new List<string>();
            var i = 0;
            name = (name.StartsWith("@") ? name : ("@" + name));
            foreach (var inClauses in values)
            {
                foreach (var inClause in inClauses)
                {
                    cmd.AddParameter($" {name}_key_{i}", inClause.Key);
                    cmd.AddParameter($"{name}_value_{i}", inClause.Value);
                    documentsAndReturnsClause.Add($" ({name}_key_{i}, {name}_value_{i})");
                    i++;
                }
            }

            var cmdInText = string.Join(", ", documentsAndReturnsClause);
            cmd.CommandText = cmd.CommandText.Replace(name, cmdInText);
        }
        public static string GetCommandTextWithParameters(this IDbCommand cmd)
        {
            return SqlBuilderUtils.GetCommandTextWithParameters(cmd.CommandText, cmd.Parameters.Cast<IDbDataParameter>().ToDictionary(k => k.ParameterName, k => k.Value));
        }
        public static string? GetNullableString(this IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return GetNullableString(reader, ordinal);
        }

        public static string? GetNullableString(this IDataReader reader, int column)
        {
            return reader.IsDBNull(column) ? reader.GetString(column) : (string?)null;
        }
        public static string GetStringOrDefault(this IDataReader reader, string columnName, string defaultValue = "")
        {
            var ordinal = reader.GetOrdinal(columnName);
            return GetStringOrDefault(reader, ordinal, defaultValue);
        }

        public static string GetStringOrDefault(this IDataReader reader, int column, string defaultValue = "")
        {
            return !reader.IsDBNull(column) ? reader.GetString(column) : defaultValue;
        }
        public static bool GetBooleanOrDefault(this IDataReader reader, string columnName, bool defaultValue = false)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return GetBooleanOrDefault(reader, ordinal, defaultValue);
        }
        public static bool GetBooleanOrDefault(this IDataReader reader, int column, bool defaultValue = false)
        {
            return !reader.IsDBNull(column) ? reader.GetBoolean(column) : defaultValue;
        }
        public static bool? GetNullableBoolean(this IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return GetNullableBoolean(reader, ordinal);
        }
        public static bool? GetNullableBoolean(this IDataReader reader, int column)
        {
            return !reader.IsDBNull(column) ? reader.GetBoolean(column) : (bool?)null;
        }
        public static int GetIntOrDefault(this IDataReader reader, string columnName, int defaultValue = 0)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return GetIntOrDefault(reader, ordinal, defaultValue);
        }
        public static int GetIntOrDefault(this IDataReader reader, int column, int defaultValue = 0)
        {
            return !reader.IsDBNull(column) ? reader.GetInt32(column) : defaultValue;
        }
        public static int? GetNullableInt(this IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return GetNullableInt(reader, ordinal);
        }
        public static int? GetNullableInt(this IDataReader reader, int column)
        {
            return !reader.IsDBNull(column) ? reader.GetInt32(column) : (int?)null;
        }
        public static long GetLongOrDefault(this IDataReader reader, string columnName, long defaultValue = 0)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return GetLongOrDefault(reader, ordinal, defaultValue);
        }
        public static long GetLongOrDefault(this IDataReader reader, int column, long defaultValue = 0)
        {
            return !reader.IsDBNull(column) ? reader.GetInt64(column) : defaultValue;
        }
        public static long? GetNullableLong(this IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return GetNullableLong(reader, ordinal);
        }
        public static long? GetNullableLong(this IDataReader reader, int column)
        {
            return !reader.IsDBNull(column) ? reader.GetInt64(column) : (long?)null;
        }
        public static Guid? GetNullableGuidFromByteArray(this IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return GetNullableGuidFromByteArray(reader, ordinal);
        }
        public static Guid? GetNullableGuidFromByteArray(this IDataReader reader, int column)
        {
            return !reader.IsDBNull(column) ? reader.GetGuidFromByteArray(column) : (Guid?)null;
        }
        public static Guid GetGuidFromByteArray(this IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return GetGuidFromByteArray(reader, ordinal);
        }
        public static Guid GetGuidFromByteArray(this IDataReader reader, int column)
        {
            var buffer = new byte[16];
            reader.GetBytes(column, 0, buffer, 0, 16);
            return new Guid(buffer);
        }
        public static DateTime GetDateTime(this IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.GetDateTime(ordinal);
        }
        public static DateTime GetDateTimeUtc(this IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return GetDateTimeUtc(reader, ordinal);
        }
        public static DateTime GetDateTimeUtc(this IDataReader reader, int column)
        {
            return DateTime.SpecifyKind(reader.GetDateTime(column), DateTimeKind.Utc);
        }
        public static DateTime GetDateTimeUtcOrDefault(this IDataReader reader, string columnName, DateTime defaultValue)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return GetDateTimeUtcOrDefault(reader, ordinal, defaultValue);
        }
        public static DateTime GetDateTimeUtcOrDefault(this IDataReader reader, int column, DateTime defaultValue)
        {
            return GetNullableDateTimeUtc(reader, column) ?? defaultValue;
        }
        public static DateTime? GetNullableDateTimeUtc(this IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return GetNullableDateTimeUtc(reader, ordinal);
        }
        public static DateTime? GetNullableDateTimeUtc(this IDataReader reader, int column)
        {
            return reader.IsDBNull(column) ? GetDateTimeUtc(reader, column):(DateTime?) null;
        }
        public static DateTime? GetNullableDateTime(this IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return GetNullableDateTime(reader, ordinal);
        }
        public static DateTime? GetNullableDateTime(this IDataReader reader, int column)
        {
            return !reader.IsDBNull(column) ? reader.GetDateTime(column):(DateTime ?) null;
        }
        public static bool IsDBNull(this IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal);
        }
    }
}