
using Microsoft.Extensions.Logging;
using Polly;

namespace Resiliency.Implementation
{
    public class PollyResiliencyPipeline : IResiliencyPipeline
    {
        private readonly IAsyncPolicy _policy;
        private readonly ILogger _logger;
        public PollyResiliencyPipeline(ILoggerFactory loggerFactory, IEnumerable<IPolicyBuilder> policyBuilders )    
        {
            _logger = loggerFactory.    CreateLogger("Resiliency");
            
            var policyBuildersArray = policyBuilders.ToArray();
 
            _policy = policyBuildersArray.Length switch
            {
                > 1 => Policy.WrapAsync(policyBuildersArray.Select(x => x.Build()).ToArray()),
                1 => policyBuildersArray[0].Build(),
                _ => Policy.NoOpAsync()
            };
        }

        public async Task Execute(Func<Task> action)
        {
            try
            {
                await _policy.ExecuteAsync(action).ConfigureAwait(false);
            }
            catch (Exception ex) 
            {
                _logger.LogWarning(ex, "Error in executing action");
            }
        }

        public async Task<T?> Execute<T>(Func<Task<T>> action, T? defaultValue = default)
        {
            try
            {
                return await _policy.ExecuteAsync(action).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in executing action. Returing to default value instead.");
                return defaultValue;
            }
        }
    }
}
