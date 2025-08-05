using InternalContracts.Implementation;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InternalContracts.Registry
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInternalContracts(this IServiceCollection services)
        {
            //services.AddScoped<IClientConfiguration, ClientConfiguration>();
            return services;
        }
    }
}
