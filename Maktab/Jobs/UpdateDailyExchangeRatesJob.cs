using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Maktab.Jobs
{
    public class UpdateDailyExchangeRatesJob : IJob
    {
        private readonly ILogger<UpdateDailyExchangeRatesJob> _logger;
       // private readonly IExchangeRatesService _exchangeRates;

        public UpdateDailyExchangeRatesJob(/*IExchangeRatesService exchangeRates,*/
        ILogger<UpdateDailyExchangeRatesJob> logger)
        {
//            _exchangeRates = exchangeRates;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
          //  return Task.CompletedTask;
            try
            {
           //     await _exchangeRates.RefreshExchangeRates().ConfigureAwait(false);
                string message = $"Exchange rates refreshsed at ${DateTime.Now.ToString()}";
                _logger.LogInformation(message);
                Debug.WriteLine(message);
            }
            catch(Exception)
            {
                throw;
            }
        }
    }
}
