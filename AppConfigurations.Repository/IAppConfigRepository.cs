using MaktabDataContracts.Requests.Configs;
using MaktabDataContracts.Responses.Configs;

namespace AppConfigurations.Repository
{
    public interface IAppConfigRepository
    {
        Task<AppConfigResponse> AddAppConfig(AddAppConfigRequest request);
        Task<AppConfigResponse> GetAppConfig(Guid appConfigId);
        Task<IEnumerable<AppConfigResponse>> GetAllAppConfigs(bool onlyActive = true);
        Task<bool> UpdateAppConfig(Guid appConfigId, UpdateAppConfigRequest request);
        Task<bool> DeleteAppConfig(Guid appConfigId, bool hardDelete = false);
    }
}
