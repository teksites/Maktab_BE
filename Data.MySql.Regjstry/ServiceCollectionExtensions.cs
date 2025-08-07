using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Polly;
using Resiliency.Registry;
using System.Configuration;

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
                   var connectionString = configuration["Database:ConnectionString"].ToString();
                   var sslCertPath = configuration["Database:SslCertPath"].ToString();

                   if (connectionString == null) 
                   {
                       throw new Exception("Database connection not found");
                   }
                   return new DatabaseConfiguration(connectionString.ToString(), sslCertPath.ToString());
               });
        }
    }
}
