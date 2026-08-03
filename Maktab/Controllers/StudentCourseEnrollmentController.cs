using Courses.Services;
using Maktab.Attributes;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Http;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Addresses;
using MaktabDataContracts.Responses.Addresses;
using Users.Services;

[Route("api/student-course-enrollments")]
[ApiController]
[EnableCors("corspolicy")]
public class StudentCourseEnrollmentController : ControllerBase
{
    private readonly IStudentCourseEnrollmentService _service;
    private readonly IDataAccessVerificationService _dataAccessVerificationService;

    public StudentCourseEnrollmentController(
        IStudentCourseEnrollmentService service,
        IDataAccessVerificationService dataAccessVerificationService)
    {
        _service = service;
        _dataAccessVerificationService = dataAccessVerificationService;
    }

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
    [HttpGet("course/{courseId:guid}")]
    public async Task<IEnumerable<StudentCourseEnrollmentResponse>> GetAllEnrollments(Guid courseId)
        => await _service.GetAllEnrollments(courseId);

    [ApiAuthorize]
    [HttpGet("recalculate-fee/family/{familyId:guid}/course/{courseId:guid}")]
    public async Task<bool> RecalculateCourseFee(Guid courseId, Guid familyId)
        => await _service.RecalculateCourseFee(courseId, familyId);

    [ApiAuthorize]
    [HttpGet("{enrollmentId:guid}")]
    public async Task<ActionResult<StudentCourseEnrollmentResponse>> GetEnrollment(Guid enrollmentId)
    {
        var enrollment = await _service.GetEnrollment(enrollmentId).ConfigureAwait(false);
        if (enrollment == null)
        {
            return NotFound();
        }

        var hasAccess = await HasFamilyEnrollmentAccessAsync(enrollment.FamilyId).ConfigureAwait(false);
        if (!hasAccess)
        {
            return Forbid();
        }

        return Ok(enrollment);
    }

    [ApiAuthorize]
    [HttpGet("family/{familyId:guid}")]
    public async Task<IEnumerable<StudentCourseEnrollmentResponse>> GetEnrollmentByFamily(Guid familyId)
        => await _service.GetEnrollmentByFamily(familyId);

    [ApiAuthorize]
    [HttpPost]
    public async Task<StudentCourseEnrollmentResponse> AddEnrollment(AddStudentCourseEnrollment enrollment)
    { 
        var response = await _service.AddEnrollment(enrollment).ConfigureAwait(false);
        
        if (response == null)
        {
            throw new BadHttpRequestException("Child already registered in the course");
        }

        return response;
    }

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin | UserRoleType.SchoolSupervisor)]
    [HttpPost("byadmin")]
    public async Task<StudentCourseEnrollmentResponse> AddEnrollmentByAdmin(AddStudentCourseEnrollment enrollment)
    {
        var response = await _service.AddEnrollment(enrollment, true).ConfigureAwait(false);

        if (response == null)
        {
            throw new BadHttpRequestException("Child already registered in the course");
        }

        return response;
    }


    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin | UserRoleType.SchoolSupervisor)]
    [HttpPut("byadmin/{enrollmentId:guid}")]
    public async Task<bool> UpdateEnrollmentByAdmin(Guid enrollmentId, AddStudentCourseEnrollment enrollment)
        => await _service.UpdateEnrollment(enrollmentId, enrollment, true);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin | UserRoleType.SchoolSupervisor)]
    [HttpPut("byadmin/batch")]
    public async Task<bool> UpdateEnrollmentsBatchByAdmin(UpdateStudentCourseEnrollmentsBatchRequest request)
        => await _service.UpdateEnrollmentsBatch(request, true);

    [ApiAuthorize]
    [HttpPut("{enrollmentId:guid}")]
    public async Task<bool> UpdateEnrollment(Guid enrollmentId, AddStudentCourseEnrollment enrollment)
        => await _service.UpdateEnrollment(enrollmentId, enrollment, false);

    [ApiAuthorize]
    [HttpPut("batch")]
    public async Task<bool> UpdateEnrollmentsBatch(UpdateStudentCourseEnrollmentsBatchRequest request)
        => await _service.UpdateEnrollmentsBatch(request, false);

    [ApiAuthorize]
    [HttpDelete("{enrollmentId:guid}")]
    public async Task<ActionResult<bool>> DeleteEnrollment(Guid enrollmentId, bool hardDelete = false)
    {
        var enrollment = await _service.GetEnrollment(enrollmentId).ConfigureAwait(false);
        if (enrollment == null)
        {
            return NotFound();
        }

        var hasAccess = await HasFamilyEnrollmentAccessAsync(enrollment.FamilyId).ConfigureAwait(false);
        if (!hasAccess)
        {
            return Forbid();
        }

        return await _service.DeleteEnrollment(enrollmentId, hardDelete, false).ConfigureAwait(false);
    }

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin | UserRoleType.SchoolSupervisor)]
    [HttpDelete("byadmin/{enrollmentId:guid}")]
    public async Task<bool> DeleteEnrollmentByAdmin(Guid enrollmentId, bool hardDelete = false)
        => await _service.DeleteEnrollment(enrollmentId, hardDelete, true);

    private async Task<bool> HasFamilyEnrollmentAccessAsync(Guid familyId)
    {
        var sessionContext = await GetRequiredSessionContext().ConfigureAwait(false);
        if (_dataAccessVerificationService.HasElevatedAccess(sessionContext.UserRoles))
        {
            return true;
        }

        return familyId == sessionContext.FamilyId;
    }

    private async Task<SessionAccessContext> GetRequiredSessionContext()
    {
        if (!Request.Headers.TryGetValue("Session_Info", out var sessionHeader)
            || !Guid.TryParse(sessionHeader, out var sessionId)
            || sessionId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Session header not found or invalid.");
        }

        var sessionContext = await _dataAccessVerificationService.GetSessionAccessContext(sessionId).ConfigureAwait(false);
        if (sessionContext == null || sessionContext.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("No active session found.");
        }

        return sessionContext;
    }
}
