using AppConfigurations.Services;
using Maktab.Attributes;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Configs;
using MaktabDataContracts.Responses.Configs;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

[Route("api/appconfigurations")]
[ApiController]
[EnableCors("corspolicy")]
public class AppConfigurationsController : ControllerBase
{
    private readonly IAppConfigService _service;

    public AppConfigurationsController(IAppConfigService service)
    {
        _service = service;
    }

    [ApiAuthorize]
    [HttpGet]
    public async Task<IEnumerable<AppConfigResponse>> GetAllAppConfigs(bool onlyActive = true)
        => await _service.GetAllAppConfigs(onlyActive).ConfigureAwait(false);

    [ApiAuthorize]
    [HttpGet("{appConfigId:guid}")]
    public async Task<AppConfigResponse> GetAppConfig(Guid appConfigId)
        => await _service.GetAppConfig(appConfigId).ConfigureAwait(false);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
    [HttpPost]
    public async Task<AppConfigResponse> AddAppConfig(AddAppConfigRequest request)
        => await _service.AddAppConfig(request).ConfigureAwait(false);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
    [HttpPut("{appConfigId:guid}")]
    public async Task<bool> UpdateAppConfig(Guid appConfigId, UpdateAppConfigRequest request)
        => await _service.UpdateAppConfig(appConfigId, request).ConfigureAwait(false);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
    [HttpDelete("{appConfigId:guid}")]
    public async Task<bool> DeleteAppConfig(Guid appConfigId, bool hardDelete = false)
        => await _service.DeleteAppConfig(appConfigId, hardDelete).ConfigureAwait(false);
}
