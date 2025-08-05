using System.Data;
namespace Data
{
    public static class DataRecordUtils
    {
        public static Guid GetIdFromBytes(IDataRecord reader, int columnIndex)
        {
            var bytes = new byte[16];
            reader.GetBytes(columnIndex, 0, bytes, 0, 16);
            return new Guid(bytes);
        }
        public static Guid? GetIdFromBytesNullable(IDataRecord reader, int columnIndex)
        {
            if (!reader.IsDBNull(columnIndex))
            {
                return GetIdFromBytes(reader, columnIndex);
            }
            return null;
        }
        public static T GetEnum<T>(IDataRecord reader, int columnIndex)
        {

            return (T)Enum.Parse(typeof(T), reader.GetString(columnIndex));
        }
    }
}