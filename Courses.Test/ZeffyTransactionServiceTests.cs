using Courses.Services;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Requests.Zeffy;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Responses.Transactions;
using Moq;
using Zeffy.Implementation.Services;
using Zeffy.Repository;

namespace Courses.Test;

public class ZeffyTransactionServiceTests
{
    [Fact]
    public async Task SaveZeffyTransaction_DefaultsPaymentTypeToCredit()
    {
        var transactionId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        AddCoursePayment? capturedPayment = null;

        var repository = new Mock<IZeffyTransactionRepository>();
        repository
            .Setup(repo => repo.Add(It.IsAny<AddZeffyRequest>()))
            .Returns(Task.CompletedTask);

        var studentTransactionService = new Mock<IStudentCourseTransactionService>();
        studentTransactionService
            .Setup(service => service.GetTransactionByPaymentCode("PAY123"))
            .ReturnsAsync(new StudentCourseTransactionResponse
            {
                StudentCourseTransactionId = transactionId,
                FamilyId = familyId
            });

        var coursePaymentService = new Mock<ICoursePaymentService>();
        coursePaymentService
            .Setup(service => service.AddPayment(It.IsAny<AddCoursePayment>()))
            .Callback<AddCoursePayment>(payment => capturedPayment = payment)
            .ReturnsAsync(new CoursePaymentResponse());

        var service = new ZeffyTransactionService(
            repository.Object,
            studentTransactionService.Object,
            coursePaymentService.Object);

        await service.SaveZeffyTransaction(new ZeffyRequest
        {
            Amount = "35.50",
            Email = "payer@example.com",
            Firstname = "Zeffy",
            Lastname = "Payer",
            CustomFields = new List<CustomField>
            {
                new()
                {
                    Question = "Payment Code",
                    Answer = "PAY123"
                }
            }
        });

        Assert.NotNull(capturedPayment);
        Assert.Equal(PaymentType.Credit, capturedPayment!.PaymentType);
    }

    [Fact]
    public async Task SaveZeffyTransaction_MapsNegativeAmountToRefund()
    {
        var transactionId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        AddCoursePayment? capturedPayment = null;

        var repository = new Mock<IZeffyTransactionRepository>();
        repository
            .Setup(repo => repo.Add(It.IsAny<AddZeffyRequest>()))
            .Returns(Task.CompletedTask);

        var studentTransactionService = new Mock<IStudentCourseTransactionService>();
        studentTransactionService
            .Setup(service => service.GetTransactionByPaymentCode("PAY124"))
            .ReturnsAsync(new StudentCourseTransactionResponse
            {
                StudentCourseTransactionId = transactionId,
                FamilyId = familyId
            });

        var coursePaymentService = new Mock<ICoursePaymentService>();
        coursePaymentService
            .Setup(service => service.AddPayment(It.IsAny<AddCoursePayment>()))
            .Callback<AddCoursePayment>(payment => capturedPayment = payment)
            .ReturnsAsync(new CoursePaymentResponse());

        var service = new ZeffyTransactionService(
            repository.Object,
            studentTransactionService.Object,
            coursePaymentService.Object);

        await service.SaveZeffyTransaction(new ZeffyRequest
        {
            Amount = "-12.00",
            Email = "payer@example.com",
            Firstname = "Zeffy",
            Lastname = "Refund",
            CustomFields = new List<CustomField>
            {
                new()
                {
                    Question = "Payment Code",
                    Answer = "PAY124"
                }
            }
        });

        Assert.NotNull(capturedPayment);
        Assert.Equal(PaymentType.Refund, capturedPayment!.PaymentType);
    }
}
