using MySql.Data.MySqlClient;
using MySql.Data.Types;
using System.Data.Common;

namespace Data.MySql
{
    public class MySqlDatabase : Database
    {
        public MySqlDatabase(DatabaseConfiguration configuration)
        : base(configuration)
        {
        }
        protected override DbConnection CreateConnection()
        {
            return new MySqlConnection(ConnectionString);
        }
        public override DbCommand CreateCommand()
        {
            return new MySqlCommand();
        }
        public override DateTime? ConvertScalarToDateTime(object value, DateTime? defaultIfInvalid = null)
        {
            if (value == null)
            {
                return null;
            }

            if (value is DateTime dateTime)
            {
                return dateTime;
            }

            if (value is MySqlDateTime mySqlDateTime)
            {
                return mySqlDateTime.IsValidDateTime ? mySqlDateTime.GetDateTime() : defaultIfInvalid;
            }

            throw new InvalidOperationException("Unable to convert value to DateTime");
        }
    }
}