using AppConfigurations.Repository;
using AppConfigurations.Services;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Configs;
using MaktabDataContracts.Responses.Configs;

namespace AppConfigurations.Implementation.Services
{
    public class AppConfigService : IAppConfigService
    {
        private readonly IAppConfigRepository _repository;

        public AppConfigService(IAppConfigRepository repository)
        {
            _repository = repository;
        }

        public Task<AppConfigResponse> AddAppConfig(AddAppConfigRequest request)
            => _repository.AddAppConfig(request);

        public Task<AppConfigResponse> GetAppConfig(Guid appConfigId)
            => _repository.GetAppConfig(appConfigId);

        public Task<IEnumerable<AppConfigResponse>> GetAllAppConfigs(bool onlyActive = true)
            => _repository.GetAllAppConfigs(onlyActive);

        public Task<AppConfigResponse> GetLatestAppConfigByType(ConfigurationType configurationType, bool onlyActive = true)
            => _repository.GetLatestAppConfigByType(configurationType, onlyActive);

        public Task<bool> UpdateAppConfig(Guid appConfigId, UpdateAppConfigRequest request)
            => _repository.UpdateAppConfig(appConfigId, request);

        public Task<bool> DeleteAppConfig(Guid appConfigId, bool hardDelete = false)
            => _repository.DeleteAppConfig(appConfigId, hardDelete);
    }
}
