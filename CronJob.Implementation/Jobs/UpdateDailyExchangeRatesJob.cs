using CronJob.Jobs;
using ExchangeRates.Services;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Diagnostics;

namespace CronJob.Implementation.Jobs
{
    public class UpdateDailyExchangeRatesJob : IJob
    {
        private readonly ILogger<UpdateDailyExchangeRatesJob> _logger;
        private readonly IExchangeRatesService _exchangeRatesService;

        UpdateDailyExchangeRatesJob(IExchangeRatesService exchangeRatesService,
        ILogger<UpdateDailyExchangeRatesJob> logger)
        {
            _exchangeRatesService = exchangeRatesService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                /*    DateTime dateTime = DateTime.Today;
                    int year = dateTime.Year;
                    int month = dateTime.Month;
                    month += 1;
                    if(month>12)
                    {
                        month = 1;
                        year += 1;
                    }
                    dateTime = new DateTime(year, month, 1);

                    var updateSuccessful =await_prayerTimeService.MonthlyAutomaticTimeUpdate(dateTime).ConfigureAwait(false);

                    string  message = "";
                    if (updateSuccessful)
                    {
                        message = $"Monthly prayer time update job executed at ${DateTime.Now.ToString()}";
                    }
                    else
                    {
                        message = $"Monthly prayer time update job FAILED at ${DateTime.Now.ToString()}";
                    }
              */
                string message = $"Monthly prayer time update job executed at ${DateTime.Now.ToString()}";


                _logger.LogInformation(message);
                Debug.WriteLine(message);
            }
            catch(Exception e)
            {
                throw;
            }
        }
    }
}
