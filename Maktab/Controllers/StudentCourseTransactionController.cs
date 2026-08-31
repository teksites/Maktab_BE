using Courses.Services;
using Helcim;
using Helcim.Services;
using Maktab.Attributes;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Helcim;
using MaktabDataContracts.Responses.Transactions;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Users.Services;

[Route("api/student-course-transactions")]
[ApiController]
[EnableCors("corspolicy")]
public class StudentCourseTransactionController : ControllerBase
{
    private readonly IStudentCourseTransactionService _service;
    private readonly IHelcimTransactionService _helcimService;
    private readonly IDataAccessVerificationService _dataAccessVerificationService;

    public StudentCourseTransactionController(
        IStudentCourseTransactionService service,
        IHelcimTransactionService helcimService,
        IDataAccessVerificationService dataAccessVerificationService)
    {
        _service = service;
        _helcimService = helcimService;
        _dataAccessVerificationService = dataAccessVerificationService;
    }

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
    [HttpGet("course/{courseId:guid}")]
    public async Task<IEnumerable<StudentCourseTransactionResponse>> GetAllCourseTransactions(Guid courseId)
        => await _service.GetAllTransactionsByCourse(courseId);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
    [HttpGet("institute/{instituteId:guid}")]
    public async Task<IEnumerable<StudentCourseTransactionResponse>> GetAllInstituteTransactions(Guid instituteId)
        => await _service.GetAllTransactionsByInstitute(instituteId);

    [ApiAuthorize()]
    [HttpGet("{transactionId:guid}")]
    public async Task<ActionResult<StudentCourseTransactionResponse>> GetTransaction(Guid transactionId)
    {
        var transaction = await _service.GetTransaction(transactionId).ConfigureAwait(false);
        if (transaction == null)
        {
            return NotFound();
        }

        var hasAccess = await HasFamilyTransactionAccessAsync(transaction.FamilyId).ConfigureAwait(false);
        if (!hasAccess)
        {
            return Forbid();
        }

        return Ok(transaction);
    }

    [ApiAuthorize()]
    [HttpGet("family/{familyId:guid}/institute/{instituteId:guid}")]
    public async Task<IEnumerable<StudentCourseTransactionResponse>> GetFamilyTransactionsByInstitute(Guid familyId, Guid instituteId)
    {
        return await _service.GetInstituteTransactionsByFamily(familyId, instituteId).ConfigureAwait(false);
    }

    [ApiAuthorize()]
    [HttpGet("family/{familyId:guid}/course/{courseId:guid}")]
    public async Task<IEnumerable<StudentCourseTransactionResponse>> GetFamilyTransactionsByCourse(Guid familyId, Guid courseId)
    {
        return await _service.GetCourseTransactionsByFamily(courseId, familyId).ConfigureAwait(false);
    }

    [ApiAuthorize()]
    [HttpGet("paymentcode/{paymentCode}")]
    public async Task<ActionResult<StudentCourseTransactionResponse>> GetTransactionByPaymentCode(string paymentCode)
    {
        var transaction = await _service.GetTransactionByPaymentCode(paymentCode).ConfigureAwait(false);
        if (transaction == null)
        {
            return NotFound();
        }

        var hasAccess = await HasFamilyTransactionAccessAsync(transaction.FamilyId).ConfigureAwait(false);
        if (!hasAccess)
        {
            return Forbid();
        }

        return Ok(transaction);
    }

    [ApiAuthorize(false, false, UserRoleType.Admin)]
    [HttpGet("family/{familyId:guid}/helcim")]
    public async Task<List<HelcimTransactionResponse>> GetHelcimTransactionsByFamilyId(Guid familyId)
    {
        return await _helcimService.GetByFamilyId(familyId).ConfigureAwait(false);
    }

    [ApiAuthorize(false, false, UserRoleType.Admin)]
    [HttpGet("family/{familyId:guid}/helcim/detailed")]
    public async Task<List<HelcimTransactionResponseDetailedView>> GetDetailedHelcimTransactionsByFamilyId(Guid familyId)
    {
        return (await _helcimService.GetDetailedByFamilyId(familyId).ConfigureAwait(false))
            .Select(item => item.ToView())
            .ToList();
    }

    [ApiAuthorize(false, false, UserRoleType.Admin)]
    [HttpGet("paymentcode/{paymentCode}/helcim")]
    public async Task<List<HelcimTransactionResponse>> GetHelcimTransactionsByPaymentCode(string paymentCode)
    {
        return await _helcimService.GetByPaymentCode(paymentCode).ConfigureAwait(false);
    }

    [ApiAuthorize(false, false, UserRoleType.Admin)]
    [HttpGet("paymentcode/{paymentCode}/helcim/detailed")]
    public async Task<List<HelcimTransactionResponseDetailedView>> GetDetailedHelcimTransactionsByPaymentCode(string paymentCode)
    {
        return (await _helcimService.GetDetailedByPaymentCode(paymentCode).ConfigureAwait(false))
            .Select(item => item.ToView())
            .ToList();
    }

    [ApiAuthorize(false, false, UserRoleType.Admin)]
    [HttpGet("transaction/{transactionId:guid}")]
    public async Task<List<HelcimTransactionResponse>> GetHelcimTransactionsByTransactionId(Guid transactionId)
    {
        return await _helcimService.GetByMaktabTransactionId(transactionId).ConfigureAwait(false);
    }

    [ApiAuthorize(false, false, UserRoleType.Admin)]
    [HttpGet("transaction/{transactionId:guid}/helcim/detailed")]
    public async Task<List<HelcimTransactionResponseDetailedView>> GetDetailedHelcimTransactionsByTransactionId(Guid transactionId)
    {
        return (await _helcimService.GetDetailedByMaktabTransactionId(transactionId).ConfigureAwait(false))
            .Select(item => item.ToView())
            .ToList();
    }

    //[HttpPost]
    //public async Task<StudentCourseTransactionResponse> AddTransaction(AddStudentCourseTransaction transaction)
    //    => await _service.AddTransaction(transaction);

    [ApiAuthorize(false, false, UserRoleType.Admin)]
    [HttpPut("{transactionId:guid}")]
    public async Task<bool> UpdateTransaction(Guid transactionId, AddStudentCourseTransaction transaction)
        => await _service.UpdateTransaction(transactionId, transaction);

    [ApiAuthorize(false, false, UserRoleType.Admin)]
    [HttpDelete("{transactionId:guid}")]
    public async Task<bool> DeleteTransaction(Guid transactionId, bool hardDelete = false)
        => await _service.DeleteTransaction(transactionId, hardDelete);

    private async Task<bool> HasFamilyTransactionAccessAsync(Guid familyId)
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
