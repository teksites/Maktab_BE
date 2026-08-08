using Courses.Services;
using Maktab.Attributes;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using MaktabDataContracts.Enums;
using Users.Services;

[Route("api/course-payments")]
[ApiController]
[EnableCors("corspolicy")]
public class CoursePaymentController : ControllerBase
{
    private readonly ICoursePaymentService _service;
    private readonly IStudentCourseTransactionService _studentCourseTransactionService;
    private readonly IDataAccessVerificationService _dataAccessVerificationService;

    public CoursePaymentController(
        ICoursePaymentService service,
        IStudentCourseTransactionService studentCourseTransactionService,
        IDataAccessVerificationService dataAccessVerificationService)
    {
        _service = service;
        _studentCourseTransactionService = studentCourseTransactionService;
        _dataAccessVerificationService = dataAccessVerificationService;
    }

    [ApiAuthorize(false, false, UserRoleType.Admin)]
    [HttpGet("course/{courseId:guid}")]
    public async Task<IEnumerable<CoursePaymentResponse>> GetAllPayments(Guid courseId)
        => await _service.GetAllPayments(courseId);

    [ApiAuthorize()]
    [HttpGet("{paymentId:guid}")]
    public async Task<ActionResult<CoursePaymentResponse>> GetPayment(Guid paymentId)
    {
        var payment = await _service.GetPayment(paymentId).ConfigureAwait(false);
        if (payment == null)
        {
            return NotFound();
        }

        var hasAccess = await HasFamilyAccessAsync(payment.FamilyId).ConfigureAwait(false);
        if (!hasAccess)
        {
            return Forbid();
        }

        return Ok(payment);
    }

    [ApiAuthorize()]
    [HttpGet("studenttransactions/{studentCourseTransactionId:guid}")]
    public async Task<ActionResult<IEnumerable<CoursePaymentResponse>>> GetPaymentByStudentCourse(Guid studentCourseTransactionId)
    {
        var transaction = await _studentCourseTransactionService.GetTransaction(studentCourseTransactionId).ConfigureAwait(false);
        if (transaction == null)
        {
            return NotFound();
        }

        var hasAccess = await HasFamilyAccessAsync(transaction.FamilyId).ConfigureAwait(false);
        if (!hasAccess)
        {
            return Forbid();
        }

        var payments = await _service.GetAllPaymentsByStudentTransactionId(studentCourseTransactionId).ConfigureAwait(false);
        return Ok(payments);
    }

    [ApiAuthorize(false, false, UserRoleType.Admin)]
    [HttpPost]
    public async Task<CoursePaymentResponse> AddPayment(AddCoursePayment payment)
        => await _service.AddPayment(payment);

    [ApiAuthorize(false, false, UserRoleType.Admin)]
    [HttpPut("{paymentId:guid}")]
    public async Task<bool> UpdatePayment(Guid paymentId, AddCoursePayment payment)
        => await _service.UpdatePayment(paymentId, payment);

    [ApiAuthorize(false, false, UserRoleType.Admin)]
    [HttpDelete("{paymentId:guid}")]
    public async Task<bool> DeletePayment(Guid paymentId, bool hardDelete = false)
        => await _service.DeletePayment(paymentId, hardDelete);

    private async Task<bool> HasFamilyAccessAsync(Guid familyId)
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
