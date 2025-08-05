
using Data;
using System;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Reflection.PortableExecutable;
namespace Cumulus.Data
{
    public abstract class DbRepository
    {
        protected IDatabase Database { get; }
        protected DbRepository(IDatabase database)
        {
            Database = database;
        }

        protected static string ReadDbFieldString(IDataReader reader, string columnName, string defValue = "")
        {
            var ordinal = reader.GetOrdinal(columnName);
            return ReadDbFieldString(reader, ordinal, defValue);
        }

        protected static string ReadDbFieldString(IDataReader reader, int column, string defValue = "")
        {
            return !reader.IsDBNull(column) ? reader.GetString(column) : defValue;
        }
        protected static bool? ReadDbFieldNullBool(IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return ReadDbFieldNullBool(reader, ordinal);
        }

        protected static bool? ReadDbFieldNullBool(IDataReader reader, int column)
        {
            return !reader.IsDBNull(column) ? new bool?(reader.GetBoolean(column)) : null;
        }
        protected static bool ReadDbFieldBool(IDataReader reader, string columnName, bool defValue = false)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return ReadDbFieldBool(reader, ordinal, defValue);
        }
        protected static bool ReadDbFieldBool(IDataReader reader, int column, bool defValue = false)
        {
            return !reader.IsDBNull(column) ? reader.GetBoolean(column) : defValue;
        }
        protected static int? ReadDbFieldNullInt(IDataReader reader, int column)
        {
            return !reader.IsDBNull(column) ? new int?(reader.GetInt32(column)) : null;
        }
        protected static int? ReadDbFieldNullInt(IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return ReadDbFieldNullInt(reader, ordinal);
        }
        protected static int ReadDbFieldInt(IDataReader reader, int column, int defValue = 0)
        {
            return !reader.IsDBNull(column) ? reader.GetInt32(column) : defValue;
        }
        protected static int ReadDbFieldInt(IDataReader reader, string columnName, int defValue = 0)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return ReadDbFieldInt(reader, ordinal, defValue);
        }

        protected static long ReadDbFieldLong(IDataReader reader, int column, long defValue = 0)
        {
            return reader.IsDBNull(column) ? reader.GetInt64(column) : defValue;
        }
        protected static T ReadDbFieldEnum<T>(IDataReader reader, int column, T defValue) where T : Enum
        {
            return !reader.IsDBNull(column) ? (T)Enum.ToObject(typeof(T), reader.GetInt32(column)) : defValue;
        }
        protected static T ReadDbFieldEnum<T>(IDataReader reader, string columnName, T defValue) where T : Enum
        {
            var ordinal = reader.GetOrdinal(columnName);
            return ReadDbFieldEnum(reader, ordinal, defValue);
        }
        protected static T ReadDbFieldEnumString<T>(IDataReader reader, int column, T defValue) where T : Enum
        {
            return !reader.IsDBNull(column) ? (T)Enum.Parse(typeof(T), reader.GetString(column)) : defValue;
        }
        protected static T ReadDbFieldEnumString<T>(IDataReader reader, string columnName, T defValue) where T : Enum
        {
            var ordinal = reader.GetOrdinal(columnName);
            return ReadDbFieldEnumString(reader, ordinal, defValue);
        }
        protected static Stream? ReadDbFieldBlob(IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return ReadDbFieldBlob(reader, ordinal);
        }
        protected static Stream? ReadDbFieldBlob(IDataReader reader, int column)
        {
            return !reader.IsDBNull(column) ? reader.ReadStream(column) : null;
        }
        protected static DateTime ReadDbFieldDateTimeUtc(IDataReader reader, string columnName, DateTime? defValue = null)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return ReadDbFieldDateTimeUTC(reader, ordinal, defValue);
        }
        protected static DateTime ReadDbFieldDateTimeUTC(IDataReader reader, int column, DateTime? defValue = null)
        {
            return !reader.IsDBNull(column) ?
                DateTime.SpecifyKind(reader.GetDateTime(column), DateTimeKind.Utc)
                : defValue ?? DateTime.MinValue;
        }

        protected static Guid ReadDbFieldGuid(IDataReader reader, int column)
        {
            var bytes0 = new byte[16];
            reader.GetBytes(column, 0, bytes0, 0, 16);
            var currentDocumentId = new Guid(bytes0);
            return currentDocumentId;
        }
        protected static Guid ReadDbFieldGuid(IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return ReadDbFieldGuid(reader, ordinal);
        }


        protected static Guid? ReadDbFieldNullableGuid(IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return ReadDbFieldNullableGuid(reader, ordinal);
        }
        protected static Guid? ReadDbFieldNullableGuid(IDataReader reader, int column)
        {
            if (reader.IsDBNull(column))
                return null;
            return ReadDbFieldGuid(reader, column);
        }
        protected static object ReadDbFieldObject(IDataReader reader, int column)
        {
            return reader.GetValue(column);
        }
        protected static object ReadDbFieldObject(IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return ReadDbFieldObject(reader, ordinal);
        }
        protected static short ReadDbFieldShort(IDataReader reader, int column, short defValue = 0)
        {
            return !reader.IsDBNull(column) ? reader.GetInt16(column) : defValue;
        }
        protected static short ReadDbFieldShort(IDataReader reader, string columnName, short defValue = 0)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return ReadDbFieldShort(reader, ordinal, defValue);
        }

        /// <summary> ///
        /// Execute action if specified column is not dbnull
        /// </summary>
        /// <param name="reader">Reader to use</param>
        /// <param name="column">Column to check</param>
        /// <param name="action">Action executed with reader and specified column passed in parameters</param> 
        public static void OnNotDBNull(IDataReader reader, int column, Action<IDataReader, int> action)
        {
            if (!reader.IsDBNull(column))
            {
                action(reader, column);
            }
        }
    }
}