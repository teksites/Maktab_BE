using Courses.Services;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Maktab.Attributes;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Users.Services;

namespace Maktab.Controllers
{
    [Route("api/course-results")]
    [ApiController]
    [EnableCors("corspolicy")]
    public class CourseResultsController : ControllerBase
    {
        private readonly IStudentCourseResultService _studentCourseResultService;
        private readonly IDataAccessVerificationService _dataAccessVerificationService;

        public CourseResultsController(
            IStudentCourseResultService studentCourseResultService,
            IDataAccessVerificationService dataAccessVerificationService)
        {
            _studentCourseResultService = studentCourseResultService;
            _dataAccessVerificationService = dataAccessVerificationService;
        }

        [ApiAuthorize(false, false, UserRoleType.Normal)]
        [HttpGet("family/{familyId:guid}")]
        public async Task<ActionResult<IReadOnlyList<StudentCourseResultResponse>>> GetFamilyResults(
            Guid familyId,
            Guid? courseId = null)
        {
            var session = await GetRequiredSessionContext().ConfigureAwait(false);
            return Ok(await _studentCourseResultService
                .GetFamilyResults(session.UserId, session.UserRoles, familyId, courseId)
                .ConfigureAwait(false));
        }

        [ApiAuthorize(false, false, UserRoleType.Normal)]
        [HttpGet("child/{childId:guid}")]
        public async Task<ActionResult<IReadOnlyList<StudentCourseResultResponse>>> GetChildResults(
            Guid childId,
            Guid? courseId = null)
        {
            var session = await GetRequiredSessionContext().ConfigureAwait(false);
            return Ok(await _studentCourseResultService
                .GetChildResults(session.UserId, session.UserRoles, childId, courseId)
                .ConfigureAwait(false));
        }

        [ApiAuthorize(false, false, UserRoleType.Assistant)]
        [HttpGet("course/{courseId:guid}")]
        public async Task<ActionResult<IReadOnlyList<StudentCourseResultResponse>>> GetCourseResults(Guid courseId)
        {
            var session = await GetRequiredSessionContext().ConfigureAwait(false);
            return Ok(await _studentCourseResultService
                .GetCourseResults(session.UserId, session.UserRoles, courseId)
                .ConfigureAwait(false));
        }

        [ApiAuthorize(false, false, UserRoleType.Normal)]
        [HttpGet("course/{courseId:guid}/child/{childId:guid}")]
        public async Task<ActionResult<StudentCourseResultResponse>> GetCourseChildResult(Guid courseId, Guid childId)
        {
            var session = await GetRequiredSessionContext().ConfigureAwait(false);
            var result = await _studentCourseResultService
                .GetCourseChildResult(session.UserId, session.UserRoles, courseId, childId)
                .ConfigureAwait(false);

            if (result == null)
            {
                return NotFound();
            }

            return Ok(result);
        }

        [ApiAuthorize(false, false, UserRoleType.Assistant)]
        [HttpPut("course/{courseId:guid}/child/{childId:guid}")]
        public async Task<ActionResult<StudentCourseResultResponse>> UpsertCourseChildResult(
            Guid courseId,
            Guid childId,
            UpsertStudentCourseResultRequest request)
        {
            var session = await GetRequiredSessionContext().ConfigureAwait(false);
            return Ok(await _studentCourseResultService
                .UpsertCourseChildResult(session.UserId, session.UserRoles, courseId, childId, request)
                .ConfigureAwait(false));
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
