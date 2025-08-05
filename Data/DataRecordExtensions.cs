using System.Data;
using System.IO;

namespace Data
{
    public static class DataRecordExtensions
    {
        public static Stream ReadStream(this IDataRecord reader, int index)
        {
            var memoryStream =
            new MemoryStream();
            var buffer = new byte[2048];
            var offset = 0;
            int numberofBytesRead;

            while ((numberofBytesRead = (int)reader.GetBytes(index, offset, buffer, 0, buffer.Length)) > 0)
            {
                memoryStream.Write(buffer, 0, numberofBytesRead);

                if (numberofBytesRead >= buffer.Length)
                {
                    offset += numberofBytesRead;
                }
                else
                {
                    break;
                }
            }
            memoryStream.Position = 0L;
            return memoryStream;
        }
    }
}