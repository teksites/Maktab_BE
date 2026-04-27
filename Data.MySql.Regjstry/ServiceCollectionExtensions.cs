using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Polly;
using Resiliency.Registry;
namespace Data.MySql.Regjstry
{
    public static class ServiceCollectionExtensions
    {
        private static readonly IEnumerable<TimeSpan> RetryTimes =
        [
            TimeSpan.FromMilliseconds(300),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
        ];
        public static IServiceCollection AddMySql(this IServiceCollection services)
        {
            return
               services
               .AddResiliencyPolicy(provider =>
                {
                    var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Data");
                    return Policy.Handle<MySqlException>(e => e.IsTransient).WaitAndRetryAsync(RetryTimes,
                        (exception, timeSpan, retryCount, _) =>
                        {
                            logger.LogWarning(exception, "Transient error while communicating with MySql database, will retry after {RetryTimeSpan}. Retry attempt {RetryCount}.",
                                timeSpan, retryCount);

                        });
                }
                )

               .AddScoped<IDatabase, MySqlDatabase>()
               .AddScoped(services =>
               {
                   var configuration = services.GetRequiredService<IConfiguration>();
                   var connectionString = ResolveConnectionString(configuration);
                   var sslCertPath = configuration["Database:SslCertPath"]?.ToString();

                   if (connectionString == null) 
                   {
                       throw new Exception("Database connection not found");
                   }
                   return new DatabaseConfiguration(connectionString, sslCertPath ?? string.Empty);
               });
        }

        private static string? ResolveConnectionString(IConfiguration configuration)
        {
            var selectedSchema = configuration["Database:ActiveSchema"];
            if (!string.IsNullOrWhiteSpace(selectedSchema))
            {
                var schemaConnectionString = configuration[$"Database:Schemas:{selectedSchema}"];
                if (!string.IsNullOrWhiteSpace(schemaConnectionString))
                {
                    return schemaConnectionString;
                }
            }

            return configuration["Database:ConnectionString"];
        }
    }
}
