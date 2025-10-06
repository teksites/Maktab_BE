using Courses.Services;
using Maktab.Attributes;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using MaktabDataContracts.Enums;

[Route("api/courses")]
[ApiController]
[EnableCors("corspolicy")]
public class CoursesController : ControllerBase
{
    private readonly ICourseService _courseService;
    private readonly ICourseEnrollmentGroupService _groupService;
    private readonly ILogger<CoursesController> _logger;

    public CoursesController(ICourseService courseService,
                             ICourseEnrollmentGroupService groupService,
                             ILogger<CoursesController> logger)
    {
        _courseService = courseService;
        _groupService = groupService;
        _logger = logger;
    }

    // COURSES
    [HttpGet]
    public async Task<IEnumerable<CourseResponseDetailed>> GetAllCourses([FromQuery] GetCourseOptions options)
        => await _courseService.GetAllCourses(options);

    [HttpGet("{courseId:guid}")]
    public async Task<CourseResponseDetailed> GetCourse(Guid courseId)
        => await _courseService.GetCourse(courseId);

    [ApiAuthorize(false, false, UserRoleType.Admin| UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
    [HttpPost]
    public async Task<CourseResponseDetailed> AddCourse(AddCourse course)
        => await _courseService.AddCourse(course);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
    [HttpPut("{courseId:guid}")]
    public async Task<CourseResponseDetailed> UpdateCourse(Guid courseId, AddCourse course)
        => await _courseService.UpdateCourse(courseId, course);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
    [HttpDelete("{courseId:guid}")]
    public async Task<bool> DeleteCourse(Guid courseId, bool hardDelete = false)
        => await _courseService.DeleteCourse(courseId, hardDelete);

    // COURSE GROUPS
    [HttpGet("{courseId:guid}/groups")]
    public async Task<IEnumerable<CourseEnrollmentGroupResponse>> GetAllGroups(Guid courseId)
        => await _groupService.GetAllCourseGroups(courseId, true);

    [HttpGet("groups/{groupId:guid}")]
    public async Task<CourseEnrollmentGroupResponse> GetGroup(Guid groupId)
        => await _groupService.GetCourseGroup(groupId);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
    [HttpPost("groups")]
    public async Task<CourseEnrollmentGroupResponse> AddGroup(AddCourseEnrollmentGroup group)
        => await _groupService.AddCourseEnrollmentGroup(group);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
    [HttpPut("groups/{groupId:guid}")]
    public async Task<bool> UpdateGroup(Guid groupId, AddCourseEnrollmentGroup group)
        => await _groupService.UpdateCourseEnrollmentGroup(groupId, group);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
    [HttpDelete("groups/{groupId:guid}")]
    public async Task<bool> DeleteGroup(Guid groupId, bool hardDelete = false)
        => await _groupService.UpdateCourseEnrollmentGroup(groupId);
}
