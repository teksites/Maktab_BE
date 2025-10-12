using Courses.Services;
using Maktab.Attributes;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using MaktabDataContracts.Responses.Transactions;

[Route("api/student-course-transactions")]
[ApiController]
[ApiAuthorize]
[EnableCors("corspolicy")]
public class StudentCourseTransactionController : ControllerBase
{
    private readonly IStudentCourseTransactionService _service;

    public StudentCourseTransactionController(IStudentCourseTransactionService service)
    {
        _service = service;
    }

    [HttpGet("course/{courseId:guid}")]
    public async Task<IEnumerable<StudentCourseTransactionResponse>> GetAllTransactions(Guid courseId)
        => await _service.GetAllTransactions(courseId);

    [HttpGet("{transactionId:guid}")]
    public async Task<StudentCourseTransactionResponse> GetTransaction(Guid transactionId)
        => await _service.GetTransaction(transactionId);

    [HttpGet("family/{familyId:guid}/institute/{instituteId:guid}")]
    public async Task<StudentCourseTransactionResponse> GetTransaction(Guid familyId, Guid instituteId)
    {
        return new StudentCourseTransactionResponse();
    }
    //=> await _service.GetTransaction(transactionId);

    [HttpPost]
    public async Task<StudentCourseTransactionResponse> AddTransaction(AddStudentCourseTransaction transaction)
        => await _service.AddTransaction(transaction);

    [HttpPut("{transactionId:guid}")]
    public async Task<bool> UpdateTransaction(Guid transactionId, AddStudentCourseTransaction transaction)
        => await _service.UpdateTransaction(transactionId, transaction);

    [HttpDelete("{transactionId:guid}")]
    public async Task<bool> DeleteTransaction(Guid transactionId, bool hardDelete = false)
        => await _service.DeleteTransaction(transactionId, hardDelete);
}
