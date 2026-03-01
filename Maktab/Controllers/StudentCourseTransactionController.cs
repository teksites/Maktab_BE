using Courses.Services;
using Maktab.Attributes;
using MaktabDataContracts.Requests.Course;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using MaktabDataContracts.Responses.Transactions;
using MaktabDataContracts.Enums;

[Route("api/student-course-transactions")]
[ApiController]
[EnableCors("corspolicy")]
public class StudentCourseTransactionController : ControllerBase
{
    private readonly IStudentCourseTransactionService _service;

    public StudentCourseTransactionController(IStudentCourseTransactionService service)
    {
        _service = service;
    }

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
    [HttpGet("course/{courseId:guid}")]
    public async Task<IEnumerable<StudentCourseTransactionResponse>> GetAllCourseTransactions(Guid courseId)
        => await _service.GetAllTransactionsByCourse(courseId);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
    [HttpGet("institute/{instituteId:guid}")]
    public async Task<IEnumerable<StudentCourseTransactionResponse>> GetAllInstituteTransactions(Guid instituteId)
        => await _service.GetAllTransactionsByInstitute(instituteId);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
    [HttpGet("{transactionId:guid}")]
    public async Task<StudentCourseTransactionResponse> GetTransaction(Guid transactionId)
        => await _service.GetTransaction(transactionId);

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
    [HttpGet("family/{familyId:guid}/institute/{instituteId:guid}")]
    public async Task<IEnumerable<StudentCourseTransactionResponse>> GetFamilyTransactionsByInstitute(Guid familyId, Guid instituteId)
    {
        return  await _service.GetInstituteTransactionsByFamily(familyId, instituteId).ConfigureAwait(false);
    }

    [ApiAuthorize(false, false, UserRoleType.Admin | UserRoleType.SuperUser | UserRoleType.SchoolAdmin)]
    [HttpGet("family/{familyId:guid}/course/{courseId:guid}")]
    public async Task<IEnumerable<StudentCourseTransactionResponse>> GetFamilyTransactionsByCourse(Guid familyId, Guid courseId)
    {
        return await _service.GetCourseTransactionsByFamily( courseId, familyId).ConfigureAwait(false);
    }

    [ApiAuthorize(false, false, UserRoleType.Admin)]
    [HttpGet("paymentcode/{paymentCode}")]
    public async Task<StudentCourseTransactionResponse> GetTransactionByPaymentCode(string paymentCode)
    {
        return await _service.GetTransactionByPaymentCode(paymentCode).ConfigureAwait(false);
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
}
