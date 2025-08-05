using Application.Users.Implementation;
using Application.Users.Repository.Implementation;
using Microsoft.Extensions.DependencyInjection;
using Users.Implementation.Services;
using Users.Repository;
using Users.Services;
using Users.Utils;
using Users.Utils.Implementation;

namespace Application.Users.Registry
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddUserServices(this IServiceCollection services)
        {
            services.AddSingleton<IAppState, AppState>();
            services.AddScoped<IUserLoginService, UserLoginService>();
            services.AddScoped<IUserLoginRepository, UserLoginRepository>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<ITempUserRepository, TempUserRepository>();
            services.AddScoped<IUserCardsService, UserCardsService>();
            services.AddScoped<IUserCardsRepository, UserCardsRepository>();
            services.AddScoped<IUserChildrenService, UserChildsService>();
            services.AddScoped<IUserChildsRepository, UserChildsRepository>();
            services.AddScoped<IAddressService, AddressService>();
            services.AddScoped<IAddressRepository, AddressRepository>();
            services.AddScoped<IExtendedUserInformationService, ExtendedUserInformationService>();
            services.AddScoped<IExtendedUserInformationRepository, ExtendedUserInformationRepository>();
 
            return services;
        }    
    }
}
