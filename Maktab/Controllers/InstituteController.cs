using Courses.Services;
using Maktab.Attributes;
using MaktabDataContracts.Requests.Institute;
using MaktabDataContracts.Responses.Institute;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using MaktabDataContracts.Enums;

[Route("api/institutes")]
[ApiController]
[EnableCors("corspolicy")]
public class InstituteController : ControllerBase
{
    private readonly IInstituteService _instituteService;
    private readonly IInstitutePolicyService _policyService;

    public InstituteController(IInstituteService instituteService,
                               IInstitutePolicyService policyService)
    {
        _instituteService = instituteService;
        _policyService = policyService;
    }

    // INSTITUTES
    [HttpGet]
    public async Task<IEnumerable<InstituteResponse>> GetAllInstitutes(bool onlyActive = true)
        => await _instituteService.GetAllInstitutes(onlyActive);

     [HttpGet("{instituteId:guid}")]
    public async Task<InstituteResponse> GetInstitute(Guid instituteId)
        => await _instituteService.GetInstitute(instituteId);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
    [HttpPost]
    public async Task<InstituteResponse> AddInstitute(AddInstitute institute)
        => await _instituteService.AddInstitute(institute);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
    [HttpPut("{instituteId:guid}")]
    public async Task<bool> UpdateInstitute(Guid instituteId, AddInstitute institute)
        => await _instituteService.UpdateInstitute(instituteId, institute);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
    [HttpDelete("{instituteId:guid}")]
    public async Task<bool> DeleteInstitute(Guid instituteId, bool hardDelete = false)
        => await _instituteService.DeleteInstitute(instituteId, hardDelete);

    // POLICIES
    [ApiAuthorize]
    [HttpGet("{instituteId:guid}/policies")]
    public async Task<IEnumerable<InstitutePolicyResponse>> GetAllPolicies(Guid instituteId)
        => await _policyService.GetAllPolicies(instituteId);

    [ApiAuthorize]
    [HttpGet("policies/{policyId:guid}")]
    public async Task<InstitutePolicyResponse> GetPolicy(Guid policyId)
        => await _policyService.GetPolicy(policyId);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
    [HttpPost("policies")]
    public async Task<InstitutePolicyResponse> AddPolicy(AddInstitutePolicy policy)
        => await _policyService.AddPolicy(policy);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
    [HttpPut("policies/{policyId:guid}")]
    public async Task<bool> UpdatePolicy(Guid policyId, AddInstitutePolicy policy)
        => await _policyService.UpdatePolicy(policyId, policy);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
    [HttpDelete("policies/{policyId:guid}")]
    public async Task<bool> DeletePolicy(Guid policyId, bool hardDelete = false)
        => await _policyService.DeletePolicy(policyId, hardDelete);
}
