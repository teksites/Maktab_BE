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

[Route("api/student-course-enrollments")]
[ApiController]
[ApiAuthorize]
[EnableCors("corspolicy")]
public class StudentCourseEnrollmentController : ControllerBase
{
    private readonly IStudentCourseEnrollmentService _service;

    public StudentCourseEnrollmentController(IStudentCourseEnrollmentService service)
    {
        _service = service;
    }

    [HttpGet("course/{courseId:guid}")]
    public async Task<IEnumerable<StudentCourseEnrollmentResponse>> GetAllEnrollments(Guid courseId)
        => await _service.GetAllEnrollments(courseId);

    [HttpGet("family/{familyId:guid}/course/{courseId:guid}")]
    public async Task<bool> RecalculateCourseFee(Guid courseId, Guid familyId)
        => await _service.RecalculateCourseFee(courseId, familyId);

    [HttpGet("{enrollmentId:guid}")]
    public async Task<StudentCourseEnrollmentResponse> GetEnrollment(Guid enrollmentId)
        => await _service.GetEnrollment(enrollmentId);

    [HttpGet("family/{familyId:guid}")]
    public async Task<IEnumerable<StudentCourseEnrollmentResponse>> GetEnrollmentByFamily(Guid familyId)
        => await _service.GetEnrollmentByFamily(familyId);

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

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin | UserRoleType.SchoolSupervoiser)]
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


    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin | UserRoleType.SchoolSupervoiser)]
    [HttpPut("byadmin/{enrollmentId:guid}")]
    public async Task<bool> UpdateEnrollmentByAdmin(Guid enrollmentId, AddStudentCourseEnrollment enrollment)
        => await _service.UpdateEnrollment(enrollmentId, enrollment, true);


    [HttpPut("{enrollmentId:guid}")]
    public async Task<bool> UpdateEnrollment(Guid enrollmentId, AddStudentCourseEnrollment enrollment)
        => await _service.UpdateEnrollment(enrollmentId, enrollment, false);

    [HttpDelete("{enrollmentId:guid}")]
    public async Task<bool> DeleteEnrollment(Guid enrollmentId, bool hardDelete = false)
        => await _service.DeleteEnrollment(enrollmentId, hardDelete, false);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin | UserRoleType.SchoolSupervoiser)]
    [HttpDelete("byadmin/{enrollmentId:guid}")]
    public async Task<bool> DeleteEnrollmentByAdmin(Guid enrollmentId, bool hardDelete = false)
        => await _service.DeleteEnrollment(enrollmentId, hardDelete, true);

}
