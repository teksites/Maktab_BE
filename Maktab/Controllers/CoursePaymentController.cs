using Courses.Services;
using Maktab.Attributes;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

[Route("api/course-payments")]
[ApiController]
[ApiAuthorize]
[EnableCors("corspolicy")]
public class CoursePaymentController : ControllerBase
{
    private readonly ICoursePaymentService _service;

    public CoursePaymentController(ICoursePaymentService service)
    {
        _service = service;
    }

    [HttpGet("course/{courseId:guid}")]
    public async Task<IEnumerable<CoursePaymentResponse>> GetAllPayments(Guid courseId)
        => await _service.GetAllPayments(courseId);

    [HttpGet("{paymentId:guid}")]
    public async Task<CoursePaymentResponse> GetPayment(Guid paymentId)
        => await _service.GetPayment(paymentId);

    [HttpGet("studenttransactions/{studentCourseTransactionId:guid}")]
    public async Task<IEnumerable<CoursePaymentResponse>> GetPaymentByStudentCourse(Guid studentCourseTransactionId)
    {
        return new List<CoursePaymentResponse>();
    }
        //  => await _service.GetPayment(paymentId);

    [HttpPost]
    public async Task<CoursePaymentResponse> AddPayment(AddCoursePayment payment)
        => await _service.AddPayment(payment);

    [HttpPut("{paymentId:guid}")]
    public async Task<bool> UpdatePayment(Guid paymentId, AddCoursePayment payment)
        => await _service.UpdatePayment(paymentId, payment);

    [HttpDelete("{paymentId:guid}")]
    public async Task<bool> DeletePayment(Guid paymentId, bool hardDelete = false)
        => await _service.DeletePayment(paymentId, hardDelete);
}
