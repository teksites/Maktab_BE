using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data
{
    public interface IDatabase
    {
        String ConnectionString { get; }
        DbConnection CreateAndOpenConnection();
        Task<DbConnection> CreateAndOpenConnectionAsync();
        DbCommand CreateCommand();
        DbCommand CreateCommand(string cmdText);
        DbCommand CreateCommand(DbConnection conn);
        DbCommand CreateCommand(string cmdText, DbConnection conn);
        DateTime? ConvertScalarToDateTime(object value, DateTime? deafaultIfInvalid = null);
    }
}
