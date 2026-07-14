using Courses.Implementation.Services;
using Courses.Repository;
using Courses.Services;
using Email;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Responses.Transactions;
using MaktabDataContracts.Responses.Users;
using Moq;
using Users.Services;

namespace Courses.Test;

public class StudentCourseTransactionServiceTests
{
    [Fact]
    public async Task UpdateTransaction_IncreasedDiscount_SendsDiscountConfirmationEmail()
    {
        var transactionId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        MultiUserEmailData? sentEmail = null;

        var repository = new Mock<IStudentCourseTransactionRepository>();
        repository
            .Setup(repo => repo.GetTransaction(transactionId))
            .ReturnsAsync(new StudentCourseTransactionResponse
            {
                StudentCourseTransactionId = transactionId,
                FamilyId = familyId,
                FeeAmountDiscount = 10m,
                DayCareDiscount = 5m,
                Enrollments = new List<StudentCourseEnrollmentResponse>
                {
                    new()
                    {
                        CourseId = courseId
                    }
                }
            });
        repository
            .Setup(repo => repo.UpdateTransaction(transactionId, It.IsAny<AddStudentCourseTransaction>()))
            .ReturnsAsync(true);

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetCourse(courseId))
            .ReturnsAsync(new CourseResponseDetailed
            {
                CourseId = courseId,
                Name = "Rattel School Quran Class (Wednesday / Friday)",
                NameFr = "l'ecole coranique Rattel (mercredi / vendredi)"
            });

        var userService = new Mock<IUserService>();
        userService
            .Setup(service => service.GetAllFamilyUsersInformation(familyId, true))
            .ReturnsAsync(new[]
            {
                new UserInformationResponse
                {
                    Email = "parent@example.com",
                    Relationship = Relationship.Mother
                }
            });

        var sendEmailService = new Mock<ISendEmailService>();
        sendEmailService
            .Setup(service => service.SendBulkEmail(It.IsAny<MultiUserEmailData>()))
            .Callback<MultiUserEmailData>(email => sentEmail = email)
            .ReturnsAsync(true);

        var service = new StudentCourseTransactionService(
            repository.Object,
            courseService.Object,
            userService.Object,
            sendEmailService.Object);

        var updated = await service.UpdateTransaction(transactionId, new AddStudentCourseTransaction
        {
            StudentCourseTransactionId = transactionId,
            FamilyId = familyId,
            FeeAmountDiscount = 25m,
            DayCareDiscount = 5m
        });

        Assert.True(updated);
        Assert.NotNull(sentEmail);
        Assert.Equal("ICC Maktab discount confirmation - confirmation de rabais ICC Maktab", sentEmail!.Subject);
        Assert.Contains("15.00", sentEmail.Body);
        Assert.Contains("A discount of", sentEmail.Body);
        Assert.Contains("Un rabais de", sentEmail.Body);
    }

    [Fact]
    public async Task UpdateTransaction_WithoutDiscountIncrease_DoesNotSendEmail()
    {
        var transactionId = Guid.NewGuid();
        var familyId = Guid.NewGuid();

        var repository = new Mock<IStudentCourseTransactionRepository>();
        repository
            .Setup(repo => repo.GetTransaction(transactionId))
            .ReturnsAsync(new StudentCourseTransactionResponse
            {
                StudentCourseTransactionId = transactionId,
                FamilyId = familyId,
                FeeAmountDiscount = 20m,
                DayCareDiscount = 5m
            });
        repository
            .Setup(repo => repo.UpdateTransaction(transactionId, It.IsAny<AddStudentCourseTransaction>()))
            .ReturnsAsync(true);

        var sendEmailService = new Mock<ISendEmailService>();

        var service = new StudentCourseTransactionService(
            repository.Object,
            Mock.Of<ICourseService>(),
            Mock.Of<IUserService>(),
            sendEmailService.Object);

        var updated = await service.UpdateTransaction(transactionId, new AddStudentCourseTransaction
        {
            StudentCourseTransactionId = transactionId,
            FamilyId = familyId,
            FeeAmountDiscount = 15m,
            DayCareDiscount = 5m
        });

        Assert.True(updated);
        sendEmailService.Verify(service => service.SendBulkEmail(It.IsAny<MultiUserEmailData>()), Times.Never);
    }

    [Fact]
    public async Task UpdateTransaction_RecalculatesTotalPayableAndClampsNegativeBalanceAfterDiscount()
    {
        var transactionId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        AddStudentCourseTransaction? capturedTransaction = null;

        var repository = new Mock<IStudentCourseTransactionRepository>();
        repository
            .Setup(repo => repo.GetTransaction(transactionId))
            .ReturnsAsync(new StudentCourseTransactionResponse
            {
                StudentCourseTransactionId = transactionId,
                FamilyId = familyId,
                FeeAmountDiscount = 0m,
                DayCareDiscount = 0m,
                TotalAmountPaid = 200m,
                FeeInstallments = new List<FeeInstallment>
                {
                    new()
                    {
                        Description = "Registration",
                        DescriptionFr = "Registration",
                        DueDate = DateTime.UtcNow.Date.AddDays(1),
                        Amount = 20m,
                        PaymentStatus = PaymentStatus.Unpaid
                    }
                },
                Enrollments = new List<StudentCourseEnrollmentResponse>
                {
                    new()
                    {
                        CourseId = courseId,
                        EnrollmentStatus = EnrollmentStatus.Cancelled
                    }
                }
            });
        repository
            .Setup(repo => repo.UpdateTransaction(transactionId, It.IsAny<AddStudentCourseTransaction>()))
            .Callback<Guid, AddStudentCourseTransaction>((_, updatedTransaction) => capturedTransaction = updatedTransaction)
            .ReturnsAsync(true);

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetCourse(courseId))
            .ReturnsAsync(new CourseResponseDetailed
            {
                CourseId = courseId,
                RegistrationFee = 20
            });

        var service = new StudentCourseTransactionService(
            repository.Object,
            courseService.Object,
            Mock.Of<IUserService>(),
            Mock.Of<ISendEmailService>());

        var updated = await service.UpdateTransaction(transactionId, new AddStudentCourseTransaction
        {
            StudentCourseTransactionId = transactionId,
            FamilyId = familyId,
            PayableFee = 0m,
            DayCareFee = 0m,
            FeeAmountDiscount = 20m,
            DayCareDiscount = 0m,
            Surcharge = 0d,
            TotalAmountPaid = 200m,
            TotalPayable = -20m
        });

        Assert.True(updated);
        Assert.NotNull(capturedTransaction);
        Assert.Equal(0m, capturedTransaction!.TotalPayable);
        Assert.Empty(capturedTransaction.FeeInstallments);
        Assert.True(capturedTransaction.IsCompletelyPaid);
    }
}
