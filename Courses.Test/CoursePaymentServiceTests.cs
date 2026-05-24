using Courses.Repository;
using Courses.Services;
using Courses.Services.Implementation;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Responses.Transactions;
using Moq;

namespace Courses.Test;

public class CoursePaymentServiceTests
{
    [Fact]
    public async Task AddPayment_WithoutExternalPaymentId_PreservesExistingZeffyStyleFlow()
    {
        var studentTransactionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var familyId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var courseId = Guid.Parse("77777777-7777-7777-7777-777777777777");

        var repository = new Mock<ICoursePaymentRepository>();
        repository
            .Setup(repo => repo.TryAddPayment(It.IsAny<AddCoursePayment>()))
            .ReturnsAsync((new CoursePaymentResponse
            {
                PaymentId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                StudentCourseTransactionId = studentTransactionId,
                FamilyId = familyId,
                AmountPaid = 50m,
                PaymentMode = PaymentMode.Zeffy,
                IsActive = true
            }, true));
        repository
            .Setup(repo => repo.GetAllPayments(studentTransactionId))
            .ReturnsAsync(new[]
            {
                new CoursePaymentResponse
                {
                    StudentCourseTransactionId = studentTransactionId,
                    FamilyId = familyId,
                    AmountPaid = 50m,
                    PaymentMode = PaymentMode.Zeffy,
                    IsActive = true
                }
            });

        var studentCourseTransactionService = new Mock<IStudentCourseTransactionService>();
        studentCourseTransactionService
            .Setup(service => service.GetTransaction(studentTransactionId))
            .ReturnsAsync(new StudentCourseTransactionResponse
            {
                StudentCourseTransactionId = studentTransactionId,
                FamilyId = familyId,
                PaymentCode = "ZEFFY1",
                Comments = "existing",
                FeeInstallments = new List<FeeInstallment>(),
                RegistrationStatus = RegistrationStatus.Pending,
                TransactionStatus = TransactionStatus.AwaitingPayment,
                IsActive = true,
                TotalPayable = 100m,
                Enrollments = new List<StudentCourseEnrollmentResponse>
                {
                    new()
                    {
                        CourseId = courseId
                    }
                }
            });
        studentCourseTransactionService
            .Setup(service => service.UpdateTransaction(studentTransactionId, It.IsAny<AddStudentCourseTransaction>()))
            .ReturnsAsync(true);

        var studentCourseEnrollmentService = new Mock<IStudentCourseEnrollmentService>();
        studentCourseEnrollmentService
            .Setup(service => service.RecalculateCourseFee(courseId, familyId))
            .ReturnsAsync(true);

        var service = new CoursePaymentService(
            repository.Object,
            studentCourseTransactionService.Object,
            studentCourseEnrollmentService.Object);

        var response = await service.AddPayment(new AddCoursePayment
        {
            StudentCourseTransactionId = studentTransactionId,
            FamilyId = familyId,
            AmountPaid = 50m,
            Comments = "Zeffy payment",
            PaymentMode = PaymentMode.Zeffy,
            IsActive = true
        });

        Assert.Equal(studentTransactionId, response.StudentCourseTransactionId);
        studentCourseTransactionService.Verify(
            service => service.UpdateTransaction(studentTransactionId, It.IsAny<AddStudentCourseTransaction>()),
            Times.Once);
        studentCourseEnrollmentService.Verify(
            service => service.RecalculateCourseFee(courseId, familyId),
            Times.Once);
    }

    [Fact]
    public async Task TryAddPayment_DuplicateExternalPaymentId_DoesNotUpdateTransactionTwice()
    {
        var studentTransactionId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var familyId = Guid.Parse("55555555-5555-5555-5555-555555555555");

        var repository = new Mock<ICoursePaymentRepository>();
        repository
            .Setup(repo => repo.TryAddPayment(It.IsAny<AddCoursePayment>()))
            .ReturnsAsync((new CoursePaymentResponse
            {
                PaymentId = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                StudentCourseTransactionId = studentTransactionId,
                FamilyId = familyId,
                AmountPaid = 99m,
                PaymentMode = PaymentMode.Helcim,
                ExternalPaymentId = "47889842",
                IsActive = true
            }, false));

        var studentCourseTransactionService = new Mock<IStudentCourseTransactionService>();
        studentCourseTransactionService
            .Setup(service => service.GetTransaction(studentTransactionId))
            .ReturnsAsync(new StudentCourseTransactionResponse
            {
                StudentCourseTransactionId = studentTransactionId,
                FamilyId = familyId,
                PaymentCode = "HEL001",
                Comments = "existing",
                FeeInstallments = new List<FeeInstallment>(),
                RegistrationStatus = RegistrationStatus.Pending,
                TransactionStatus = TransactionStatus.AwaitingPayment,
                IsActive = true,
                TotalPayable = 100m
            });

        var service = new CoursePaymentService(
            repository.Object,
            studentCourseTransactionService.Object,
            Mock.Of<IStudentCourseEnrollmentService>());

        var result = await service.TryAddPayment(new AddCoursePayment
        {
            StudentCourseTransactionId = studentTransactionId,
            FamilyId = familyId,
            AmountPaid = 99m,
            Comments = "Helcim payment",
            ExternalPaymentId = "47889842",
            PaymentMode = PaymentMode.Helcim,
            IsActive = true
        });

        Assert.False(result.Created);
        studentCourseTransactionService.Verify(
            service => service.UpdateTransaction(It.IsAny<Guid>(), It.IsAny<AddStudentCourseTransaction>()),
            Times.Never);
    }
}
