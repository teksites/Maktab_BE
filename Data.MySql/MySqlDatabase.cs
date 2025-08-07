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
            string certPath = Path.GetFullPath(SslCertPath);

            if (!File.Exists(certPath))
                throw new FileNotFoundException("SSL Certificate not found at: " + certPath);


            //return new MySqlConnection(ConnectionString);
            //string[] possiblePaths =
            //{
            //    Path.Combine(AppContext.BaseDirectory, "Content", "DigiCertGlobalRootCA.crt.pem"),
            //    Path.Combine(Directory.GetCurrentDirectory(), "Content", "DigiCertGlobalRootCA.crt.pem")
            //};

            //string certPath = possiblePaths.FirstOrDefault(File.Exists)
            //    ?? throw new FileNotFoundException("SSL Certificate not found in expected paths.");

            var builder = new MySqlConnectionStringBuilder(ConnectionString)
            {
                SslMode = MySqlSslMode.VerifyCA,
                SslCa = certPath
            };

            return new MySqlConnection(builder.ConnectionString);
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