using Courses.Services;
using Maktab.Attributes;
using MaktabDataContracts.Enums;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AddCourseGroupPreRequisite = MaktabDataContracts.Requests.Course.AddCourseGroupPreRequisite;
using CourseGroupPreRequisiteResponse = MaktabDataContracts.Responses.Course.CourseGroupPreRequisiteResponse;

[Route("api/course-group-prerequisites")]
[ApiController]
[EnableCors("corspolicy")]
public class CourseGroupPreRequisiteController : ControllerBase
{
    private readonly ICourseGroupPreRequisiteService _service;

    public CourseGroupPreRequisiteController(ICourseGroupPreRequisiteService service)
    {
        _service = service;
    }

    [HttpGet("{courseGroupPreRequisiteId:guid}")]
    public Task<CourseGroupPreRequisiteResponse?> Get(Guid courseGroupPreRequisiteId)
        => _service.Get(courseGroupPreRequisiteId);

    [HttpGet("groups/{courseGroupId:guid}")]
    public Task<IEnumerable<CourseGroupPreRequisiteResponse>> GetByCourseGroup(Guid courseGroupId, bool onlyActive = true)
        => _service.GetByCourseGroup(courseGroupId, onlyActive);

    [HttpGet("enrollments/{enrollmentId:guid}")]
    public Task<IEnumerable<CourseGroupPreRequisiteResponse>> GetByEnrollment(Guid enrollmentId, bool onlyActive = true)
        => _service.GetByEnrollment(enrollmentId, onlyActive);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
    [HttpPost]
    public Task<CourseGroupPreRequisiteResponse> Add(AddCourseGroupPreRequisite preRequisite)
        => _service.Add(preRequisite);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
    [HttpPut("{courseGroupPreRequisiteId:guid}")]
    public Task<bool> Update(Guid courseGroupPreRequisiteId, AddCourseGroupPreRequisite preRequisite)
        => _service.Update(courseGroupPreRequisiteId, preRequisite);

    [ApiAuthorize(false, false, UserRoleType.Admin)]
    [HttpDelete("{courseGroupPreRequisiteId:guid}")]
    public Task<bool> Delete(Guid courseGroupPreRequisiteId, bool hardDelete = false)
        => _service.Delete(courseGroupPreRequisiteId, hardDelete);
}
