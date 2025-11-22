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
    
    [HttpPut("{enrollmentId:guid}")]
    public async Task<bool> UpdateEnrollment(Guid enrollmentId, AddStudentCourseEnrollment enrollment)
        => await _service.UpdateEnrollment(enrollmentId, enrollment);

    [HttpDelete("{enrollmentId:guid}")]
    public async Task<bool> DeleteEnrollment(Guid enrollmentId, bool hardDelete = false)
        => await _service.DeleteEnrollment(enrollmentId, hardDelete);
}
