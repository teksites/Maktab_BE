namespace Data
{
    public class DatabaseConfiguration
    {
        public readonly string ConnectionString;
        public readonly string SSLCertPath;

        public DatabaseConfiguration(string connectionString, string SslCertPath) 
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException(nameof (connectionString));
            }
            ConnectionString = connectionString;
            SSLCertPath = SslCertPath;
        }
    }
}