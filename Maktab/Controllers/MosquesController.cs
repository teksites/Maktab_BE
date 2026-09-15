using Courses.Services;
using Maktab.Attributes;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Institute;
using MaktabDataContracts.Responses.Institute;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

[Route("api/mosques")]
[ApiController]
[EnableCors("corspolicy")]
public class MosquesController : ControllerBase
{
    private readonly IInstituteService _instituteService;

    public MosquesController(IInstituteService instituteService)
    {
        _instituteService = instituteService;
    }

    [HttpGet]
    public Task<IEnumerable<InstituteResponse>> GetAllMosques(bool onlyActive = true)
        => _instituteService.GetAllInstitutes(onlyActive, InstituteType.Mosque);

    [HttpGet("{mosqueId:guid}")]
    public async Task<ActionResult<InstituteResponse>> GetMosque(Guid mosqueId)
    {
        var mosque = await _instituteService.GetInstitute(mosqueId);
        return mosque?.InstituteType == InstituteType.Mosque ? Ok(mosque) : NotFound();
    }

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
    [HttpPost]
    public Task<InstituteResponse> AddMosque(AddInstitute mosque)
    {
        mosque.InstituteType = InstituteType.Mosque;
        return _instituteService.AddInstitute(mosque);
    }

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
    [HttpPut("{mosqueId:guid}")]
    public async Task<ActionResult<bool>> UpdateMosque(Guid mosqueId, AddInstitute mosque)
    {
        var existingMosque = await _instituteService.GetInstitute(mosqueId);
        if (existingMosque?.InstituteType != InstituteType.Mosque)
        {
            return NotFound();
        }

        mosque.InstituteType = InstituteType.Mosque;
        return Ok(await _instituteService.UpdateInstitute(mosqueId, mosque));
    }

    [ApiAuthorize(false, false, UserRoleType.Admin)]
    [HttpDelete("{mosqueId:guid}")]
    public async Task<ActionResult<bool>> DeleteMosque(Guid mosqueId, bool hardDelete = false)
    {
        var existingMosque = await _instituteService.GetInstitute(mosqueId);
        if (existingMosque?.InstituteType != InstituteType.Mosque)
        {
            return NotFound();
        }

        return Ok(await _instituteService.DeleteInstitute(mosqueId, hardDelete));
    }
}
