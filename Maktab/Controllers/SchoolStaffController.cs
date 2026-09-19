using Courses.Services;
using Maktab.Attributes;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Attendance;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Requests.InstituteStaff;
using MaktabDataContracts.Responses.Attendance;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Responses.InstituteStaff;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Users.Services;

namespace Maktab.Controllers
{
    [Route("api/staff")]
    [ApiController]
    [EnableCors("corspolicy")]
    public class SchoolStaffController : ControllerBase
    {
        private readonly IInstituteStaffAssignmentService _instituteStaffAssignmentService;
        private readonly ICourseStaffAssignmentService _courseStaffAssignmentService;
        private readonly IStudentCourseAttendanceService _studentCourseAttendanceService;
        private readonly IDataAccessVerificationService _dataAccessVerificationService;

        public SchoolStaffController(
            IInstituteStaffAssignmentService instituteStaffAssignmentService,
            ICourseStaffAssignmentService courseStaffAssignmentService,
            IStudentCourseAttendanceService studentCourseAttendanceService,
            IDataAccessVerificationService dataAccessVerificationService)
        {
            _instituteStaffAssignmentService = instituteStaffAssignmentService;
            _courseStaffAssignmentService = courseStaffAssignmentService;
            _studentCourseAttendanceService = studentCourseAttendanceService;
            _dataAccessVerificationService = dataAccessVerificationService;
        }

        [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin | UserRoleType.SchoolSupervisor)]
        [HttpPost("institutes/assignments/search")]
        public async Task<ActionResult<IEnumerable<InstituteStaffAssignmentResponse>>> GetInstituteStaffAssignments(GetInstituteStaffRequest request)
            => await Execute<IEnumerable<InstituteStaffAssignmentResponse>>(async () => Ok(await _instituteStaffAssignmentService.GetAssignments(request).ConfigureAwait(false))).ConfigureAwait(false);

        [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin | UserRoleType.SchoolSupervisor)]
        [HttpPost("institutes/candidates/search")]
        public async Task<ActionResult<IEnumerable<InstituteStaffCandidateResponse>>> GetInstituteStaffCandidates(GetInstituteStaffCandidatesRequest request)
            => await Execute<IEnumerable<InstituteStaffCandidateResponse>>(async () => Ok(await _instituteStaffAssignmentService.GetCandidates(request).ConfigureAwait(false))).ConfigureAwait(false);

        [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
        [HttpPost("institutes/assignments")]
        public async Task<ActionResult<InstituteStaffAssignmentResponse>> AddInstituteStaffAssignment(AddInstituteStaffAssignmentRequest request)
            => await Execute<InstituteStaffAssignmentResponse>(async () => Ok(await _instituteStaffAssignmentService.AddAssignment(request).ConfigureAwait(false))).ConfigureAwait(false);

        [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
        [HttpPut("institutes/assignments/{assignmentId:guid}")]
        public async Task<ActionResult<InstituteStaffAssignmentResponse>> UpdateInstituteStaffAssignment(Guid assignmentId, UpdateInstituteStaffAssignmentRequest request)
            => await Execute<InstituteStaffAssignmentResponse>(async () => Ok(await _instituteStaffAssignmentService.UpdateAssignment(assignmentId, request).ConfigureAwait(false))).ConfigureAwait(false);

        [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
        [HttpDelete("institutes/assignments/{assignmentId:guid}")]
        public async Task<ActionResult<bool>> DeleteInstituteStaffAssignment(Guid assignmentId, bool hardDelete = false)
            => await Execute<bool>(async () => Ok(await _instituteStaffAssignmentService.DeleteAssignment(assignmentId, hardDelete).ConfigureAwait(false))).ConfigureAwait(false);

        [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin | UserRoleType.SchoolSupervisor)]
        [HttpGet("courses/{courseId:guid}/assignments")]
        public async Task<ActionResult<CourseStaffAssignmentsResponse>> GetCourseStaffAssignments(Guid courseId, bool onlyActive = true)
            => await Execute<CourseStaffAssignmentsResponse>(async () => Ok(await _courseStaffAssignmentService.GetCourseAssignments(courseId, onlyActive).ConfigureAwait(false))).ConfigureAwait(false);

        [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
        [HttpPut("courses/{courseId:guid}/assignments")]
        public async Task<ActionResult<CourseStaffAssignmentsResponse>> SetCourseStaffAssignments(Guid courseId, SetCourseStaffAssignmentsRequest request)
        {
            request.CourseId = courseId;
            return await Execute<CourseStaffAssignmentsResponse>(async () => Ok(await _courseStaffAssignmentService.SetCourseAssignments(request).ConfigureAwait(false))).ConfigureAwait(false);
        }

        [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin | UserRoleType.SchoolSupervisor)]
        [HttpGet("course-groups/{courseEnrollmentGroupId:guid}/assignments")]
        public async Task<ActionResult<CourseGroupStaffAssignmentsResponse>> GetCourseGroupStaffAssignments(Guid courseEnrollmentGroupId, bool onlyActive = true)
            => await Execute<CourseGroupStaffAssignmentsResponse>(async () => Ok(await _courseStaffAssignmentService.GetCourseGroupAssignments(courseEnrollmentGroupId, onlyActive).ConfigureAwait(false))).ConfigureAwait(false);

        [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
        [HttpPut("course-groups/{courseEnrollmentGroupId:guid}/assignments")]
        public async Task<ActionResult<CourseGroupStaffAssignmentsResponse>> SetCourseGroupStaffAssignments(Guid courseEnrollmentGroupId, SetCourseGroupStaffAssignmentsRequest request)
        {
            request.CourseEnrollmentGroupId = courseEnrollmentGroupId;
            return await Execute<CourseGroupStaffAssignmentsResponse>(async () => Ok(await _courseStaffAssignmentService.SetCourseGroupAssignments(request).ConfigureAwait(false))).ConfigureAwait(false);
        }

        [ApiAuthorize(false, false, UserRoleType.Assistant)]
        [HttpGet("me/course-groups")]
        public async Task<ActionResult<IEnumerable<TeacherAssignedCourseGroupResponse>>> GetMyAssignedCourseGroups(bool onlyActive = true)
        {
            return await Execute<IEnumerable<TeacherAssignedCourseGroupResponse>>(async () =>
            {
                var session = await GetRequiredSessionContext().ConfigureAwait(false);
                return Ok(await _courseStaffAssignmentService.GetAssignedCourseGroups(session.UserId, onlyActive).ConfigureAwait(false));
            }).ConfigureAwait(false);
        }

        [ApiAuthorize(false, false, UserRoleType.Assistant)]
        [HttpGet("me/course-groups/{courseEnrollmentGroupId:guid}/roster")]
        public async Task<ActionResult<TeacherCourseGroupRosterResponse>> GetMyCourseGroupRoster(Guid courseEnrollmentGroupId)
        {
            return await Execute<TeacherCourseGroupRosterResponse>(async () =>
            {
                var session = await GetRequiredSessionContext().ConfigureAwait(false);
                return Ok(await _courseStaffAssignmentService.GetAssignedCourseGroupRoster(session.UserId, session.UserRoles, courseEnrollmentGroupId).ConfigureAwait(false));
            }).ConfigureAwait(false);
        }

        [ApiAuthorize(false, false, UserRoleType.Assistant)]
        [HttpGet("me/course-groups/{courseEnrollmentGroupId:guid}/attendance")]
        public async Task<ActionResult<CourseGroupAttendanceResponse>> GetMyCourseGroupAttendance(Guid courseEnrollmentGroupId, DateTime attendanceDate, Guid? childId = null)
        {
            return await Execute<CourseGroupAttendanceResponse>(async () =>
            {
                var session = await GetRequiredSessionContext().ConfigureAwait(false);
                return Ok(await _studentCourseAttendanceService.GetCourseGroupAttendance(session.UserId, session.UserRoles, new GetCourseGroupAttendanceRequest
                {
                    CourseEnrollmentGroupId = courseEnrollmentGroupId,
                    AttendanceDate = attendanceDate,
                    ChildId = childId
                }).ConfigureAwait(false));
            }).ConfigureAwait(false);
        }

        [ApiAuthorize(false, false, UserRoleType.Assistant)]
        [HttpPut("me/course-groups/{courseEnrollmentGroupId:guid}/attendance")]
        public async Task<ActionResult<CourseGroupAttendanceResponse>> UpsertMyCourseGroupAttendance(Guid courseEnrollmentGroupId, UpsertCourseGroupAttendanceRequest request)
        {
            request.CourseEnrollmentGroupId = courseEnrollmentGroupId;

            return await Execute<CourseGroupAttendanceResponse>(async () =>
            {
                var session = await GetRequiredSessionContext().ConfigureAwait(false);
                return Ok(await _studentCourseAttendanceService.UpsertCourseGroupAttendance(session.UserId, session.UserRoles, request).ConfigureAwait(false));
            }).ConfigureAwait(false);
        }

        [ApiAuthorize(false, false, UserRoleType.Assistant)]
        [HttpPut("me/course-groups/{courseEnrollmentGroupId:guid}/attendance/students/{studentCourseEnrollmentId:guid}")]
        public async Task<ActionResult<StudentCourseAttendanceResponse>> UpsertMyStudentAttendance(
            Guid courseEnrollmentGroupId,
            Guid studentCourseEnrollmentId,
            UpsertStudentAttendanceRequest request)
        {
            return await Execute<StudentCourseAttendanceResponse>(async () =>
            {
                var session = await GetRequiredSessionContext().ConfigureAwait(false);
                return Ok(await _studentCourseAttendanceService.UpsertStudentAttendance(
                    session.UserId,
                    session.UserRoles,
                    courseEnrollmentGroupId,
                    studentCourseEnrollmentId,
                    request).ConfigureAwait(false));
            }).ConfigureAwait(false);
        }

        private async Task<ActionResult<T>> Execute<T>(Func<Task<ActionResult<T>>> action)
        {
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
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
}
