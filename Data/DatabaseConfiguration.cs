namespace Data
{
    public class DatabaseConfiguration
    {
        public readonly string ConnectionString;
        
        public DatabaseConfiguration(string connectionString) 
        {
            if(string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException(nameof (connectionString));
            }
            ConnectionString = connectionString;
        }
    }
}