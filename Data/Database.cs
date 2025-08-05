using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Data
{
    public abstract class Database : IDatabase
    {
        private readonly DatabaseConfiguration _configuration;
        public string ConnectionString => _configuration.ConnectionString;

        protected Database(DatabaseConfiguration configuration)
        {
            _configuration = configuration;
        }
        protected abstract DbConnection CreateConnection();

        public DbConnection CreateAndOpenConnection()
        {
            var conn = CreateConnection();
            conn.Open();
            return conn;
        }

        public async Task<DbConnection> CreateAndOpenConnectionAsync()
        {
            var conn = CreateConnection();
            await conn.OpenAsync().ConfigureAwait(false);
            return conn;
        }

        public abstract DbCommand CreateCommand();

        public DbCommand CreateCommand(string cmdText)
        {
            return CreateCommand(cmdText, null);
        }
        public DbCommand CreateCommand(DbConnection conn)
        {
            return CreateCommand(null, conn);
        }
        public DbCommand CreateCommand(string? cmdText, DbConnection? conn)
        {
            var cmd = CreateCommand();
            cmd.Connection = conn;
            cmd.CommandText = cmdText;
            return cmd;
        }
        public abstract DateTime? ConvertScalarToDateTime(object value, DateTime? defaultIfInvalid = null);

    }
}
