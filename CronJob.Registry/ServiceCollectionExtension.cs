using CronJob.Implementation.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace CronJob.Registry
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddJobSchedularServices(this IServiceCollection services)
        {
            /*
            services.AddQuartz(q =>
            {
                // base Quartz scheduler, job and trigger configuration
            });

            // ASP.NET Core hosting
            services.AddQuartzServer(options =>
            {
                // when shutting down we want jobs to complete gracefully
                options.WaitForJobsToComplete = true;
            });

            services.AddQuartz(q =>
            {
                q.UseMicrosoftDependencyInjectionScopedJobFactory();
                var jobKey = new JobKey("UpdateDailyExchangeRatesJob");
                q.AddJob<UpdateDailyExchangeRatesJob>(opts => opts.WithIdentity(jobKey));

                q.AddTrigger(opts => opts
                    .ForJob(jobKey)
                    .WithIdentity("UpdateDailyExchangeRatesJob-trigger")
                    .WithCronSchedule("0/5 * * * * ?"));

            });

            services.AddQuartzServer(q => q.WaitForJobsToComplete = true);
           */ return services;
        }

    }
}
