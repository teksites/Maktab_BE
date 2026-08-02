using Courses.Services;
using Maktab.Attributes;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Attendance;
using MaktabDataContracts.Responses.Attendance;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Users.Services;

namespace Maktab.Controllers
{
    [Route("api/attendance")]
    [ApiController]
    [EnableCors("corspolicy")]
    public class AttendanceController : ControllerBase
    {
        private readonly IStudentCourseAttendanceService _studentCourseAttendanceService;
        private readonly IDataAccessVerificationService _dataAccessVerificationService;

        public AttendanceController(
            IStudentCourseAttendanceService studentCourseAttendanceService,
            IDataAccessVerificationService dataAccessVerificationService)
        {
            _studentCourseAttendanceService = studentCourseAttendanceService;
            _dataAccessVerificationService = dataAccessVerificationService;
        }

        [ApiAuthorize(false, false, UserRoleType.Normal)]
        [HttpPost("families/{familyId:guid}/records")]
        public async Task<ActionResult<IEnumerable<AttendanceRecordResponse>>> GetFamilyAttendanceRecords(Guid familyId, GetAttendanceRecordsRequest request)
        {
            request.FamilyId = familyId;

            return await Execute<IEnumerable<AttendanceRecordResponse>>(async () =>
                Ok(await _studentCourseAttendanceService.GetFamilyAttendanceRecords(familyId, request).ConfigureAwait(false))).ConfigureAwait(false);
        }

        [ApiAuthorize(false, false, UserRoleType.Normal)]
        [HttpPost("families/{familyId:guid}/report")]
        public async Task<ActionResult<AttendanceReportResponse>> GetFamilyAttendanceReport(Guid familyId, GetAttendanceReportRequest request)
        {
            request.FamilyId = familyId;

            return await Execute<AttendanceReportResponse>(async () =>
                Ok(await _studentCourseAttendanceService.GetFamilyAttendanceReport(familyId, request).ConfigureAwait(false))).ConfigureAwait(false);
        }

        [ApiAuthorize(false, false, UserRoleType.Assistant)]
        [HttpPost("staff/records")]
        public async Task<ActionResult<IEnumerable<AttendanceRecordResponse>>> GetStaffAttendanceRecords(GetAttendanceRecordsRequest request)
        {
            return await Execute<IEnumerable<AttendanceRecordResponse>>(async () =>
            {
                var session = await GetRequiredSessionContext().ConfigureAwait(false);
                return Ok(await _studentCourseAttendanceService.GetStaffAttendanceRecords(session.UserId, session.UserRoles, request).ConfigureAwait(false));
            }).ConfigureAwait(false);
        }

        [ApiAuthorize(false, false, UserRoleType.Assistant)]
        [HttpPost("staff/report")]
        public async Task<ActionResult<AttendanceReportResponse>> GetStaffAttendanceReport(GetAttendanceReportRequest request)
        {
            return await Execute<AttendanceReportResponse>(async () =>
            {
                var session = await GetRequiredSessionContext().ConfigureAwait(false);
                return Ok(await _studentCourseAttendanceService.GetStaffAttendanceReport(session.UserId, session.UserRoles, request).ConfigureAwait(false));
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
