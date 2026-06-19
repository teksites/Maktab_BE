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
        AddCoursePayment? capturedPayment = null;

        var repository = new Mock<ICoursePaymentRepository>();
        repository
            .Setup(repo => repo.TryAddPayment(It.IsAny<AddCoursePayment>()))
            .Callback<AddCoursePayment>(payment => capturedPayment = payment)
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
        Assert.NotNull(capturedPayment);
        Assert.Equal(PaymentType.Credit, capturedPayment!.PaymentType);
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
        repository
            .Setup(repo => repo.GetAllPayments(studentTransactionId))
            .ReturnsAsync(Array.Empty<CoursePaymentResponse>());

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

    [Fact]
    public async Task AddPayment_PreservesExplicitPaymentTypeFromPayload()
    {
        var studentTransactionId = Guid.Parse("12121212-1212-1212-1212-121212121212");
        var familyId = Guid.Parse("34343434-3434-3434-3434-343434343434");
        AddCoursePayment? capturedPayment = null;

        var repository = new Mock<ICoursePaymentRepository>();
        repository
            .Setup(repo => repo.TryAddPayment(It.IsAny<AddCoursePayment>()))
            .Callback<AddCoursePayment>(payment => capturedPayment = payment)
            .ReturnsAsync((new CoursePaymentResponse
            {
                PaymentId = Guid.NewGuid(),
                StudentCourseTransactionId = studentTransactionId,
                FamilyId = familyId,
                AmountPaid = 15m,
                PaymentType = PaymentType.Refund,
                PaymentMode = PaymentMode.CashOnCounter,
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
                    AmountPaid = 15m,
                    PaymentType = PaymentType.Refund,
                    PaymentMode = PaymentMode.CashOnCounter,
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
                PaymentCode = "MANUAL1",
                Comments = "existing",
                FeeInstallments = new List<FeeInstallment>(),
                RegistrationStatus = RegistrationStatus.Pending,
                TransactionStatus = TransactionStatus.AwaitingPayment,
                IsActive = true,
                TotalPayable = 100m
            });
        studentCourseTransactionService
            .Setup(service => service.UpdateTransaction(studentTransactionId, It.IsAny<AddStudentCourseTransaction>()))
            .ReturnsAsync(true);

        var service = new CoursePaymentService(
            repository.Object,
            studentCourseTransactionService.Object,
            Mock.Of<IStudentCourseEnrollmentService>());

        await service.AddPayment(new AddCoursePayment
        {
            StudentCourseTransactionId = studentTransactionId,
            FamilyId = familyId,
            AmountPaid = 15m,
            Comments = "manual refund",
            PaymentMode = PaymentMode.CashOnCounter,
            PaymentType = PaymentType.Refund,
            IsActive = true
        });

        Assert.NotNull(capturedPayment);
        Assert.Equal(PaymentType.Refund, capturedPayment!.PaymentType);
    }

    [Fact]
    public async Task AddPayment_Refund_DeductsFromTotalAmountPaid()
    {
        var studentTransactionId = Guid.Parse("abababab-abab-abab-abab-abababababab");
        var familyId = Guid.Parse("cdcdcdcd-cdcd-cdcd-cdcd-cdcdcdcdcdcd");
        var updatedTransactions = new List<AddStudentCourseTransaction>();

        var repository = new Mock<ICoursePaymentRepository>();
        repository
            .Setup(repo => repo.TryAddPayment(It.IsAny<AddCoursePayment>()))
            .ReturnsAsync((new CoursePaymentResponse
            {
                PaymentId = Guid.NewGuid(),
                StudentCourseTransactionId = studentTransactionId,
                FamilyId = familyId,
                AmountPaid = 20m,
                PaymentType = PaymentType.Refund,
                PaymentMode = PaymentMode.CashOnCounter,
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
                    AmountPaid = 100m,
                    PaymentType = PaymentType.Credit,
                    PaymentMode = PaymentMode.CashOnCounter,
                    IsActive = true
                },
                new CoursePaymentResponse
                {
                    StudentCourseTransactionId = studentTransactionId,
                    FamilyId = familyId,
                    AmountPaid = 20m,
                    PaymentType = PaymentType.Refund,
                    PaymentMode = PaymentMode.CashOnCounter,
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
                PaymentCode = "REFUND1",
                Comments = "existing",
                FeeInstallments = new List<FeeInstallment>(),
                RegistrationStatus = RegistrationStatus.Pending,
                TransactionStatus = TransactionStatus.FullyPaid,
                IsActive = true,
                TotalPayable = 100m,
                TotalAmountPaid = 100m
            });
        studentCourseTransactionService
            .Setup(service => service.UpdateTransaction(studentTransactionId, It.IsAny<AddStudentCourseTransaction>()))
            .Callback<Guid, AddStudentCourseTransaction>((_, transaction) => updatedTransactions.Add(transaction))
            .ReturnsAsync(true);

        var service = new CoursePaymentService(
            repository.Object,
            studentCourseTransactionService.Object,
            Mock.Of<IStudentCourseEnrollmentService>());

        await service.AddPayment(new AddCoursePayment
        {
            StudentCourseTransactionId = studentTransactionId,
            FamilyId = familyId,
            AmountPaid = 20m,
            Comments = "refund",
            PaymentMode = PaymentMode.CashOnCounter,
            PaymentType = PaymentType.Refund,
            IsActive = true
        });

        var updated = Assert.Single(updatedTransactions);
        Assert.Equal(80m, updated.TotalAmountPaid);
        Assert.Equal(100m, updated.TotalPayable);
        Assert.False(updated.IsCompletelyPaid);
        Assert.Equal(TransactionStatus.PartiallyPaid, updated.TransactionStatus);
    }

    [Fact]
    public async Task UpdatePayment_Refund_DeductsFromTotalAmountPaid()
    {
        var paymentId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var studentTransactionId = Guid.Parse("12111111-2222-3333-4444-555555555555");
        var familyId = Guid.Parse("66666666-7777-8888-9999-000000000000");
        var updatedTransactions = new List<AddStudentCourseTransaction>();

        var repository = new Mock<ICoursePaymentRepository>();
        repository
            .Setup(repo => repo.UpdatePayment(paymentId, It.IsAny<AddCoursePayment>()))
            .ReturnsAsync(true);
        repository
            .Setup(repo => repo.GetAllPayments(studentTransactionId))
            .ReturnsAsync(new[]
            {
                new CoursePaymentResponse
                {
                    StudentCourseTransactionId = studentTransactionId,
                    FamilyId = familyId,
                    AmountPaid = 100m,
                    PaymentType = PaymentType.Credit,
                    PaymentMode = PaymentMode.CashOnCounter,
                    IsActive = true
                },
                new CoursePaymentResponse
                {
                    StudentCourseTransactionId = studentTransactionId,
                    FamilyId = familyId,
                    AmountPaid = 20m,
                    PaymentType = PaymentType.Refund,
                    PaymentMode = PaymentMode.CashOnCounter,
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
                PaymentCode = "MANUAL-UPDATE-REFUND",
                Comments = "existing",
                FeeInstallments = new List<FeeInstallment>(),
                RegistrationStatus = RegistrationStatus.Pending,
                TransactionStatus = TransactionStatus.FullyPaid,
                IsActive = true,
                TotalPayable = 100m,
                TotalAmountPaid = 100m
            });
        studentCourseTransactionService
            .Setup(service => service.UpdateTransaction(studentTransactionId, It.IsAny<AddStudentCourseTransaction>()))
            .Callback<Guid, AddStudentCourseTransaction>((_, transaction) => updatedTransactions.Add(transaction))
            .ReturnsAsync(true);

        var service = new CoursePaymentService(
            repository.Object,
            studentCourseTransactionService.Object,
            Mock.Of<IStudentCourseEnrollmentService>());

        var updated = await service.UpdatePayment(paymentId, new AddCoursePayment
        {
            StudentCourseTransactionId = studentTransactionId,
            FamilyId = familyId,
            AmountPaid = 20m,
            Comments = "manual refund update",
            PaymentMode = PaymentMode.CashOnCounter,
            PaymentType = PaymentType.Refund,
            IsActive = true
        });

        Assert.True(updated);
        var recalculatedTransaction = Assert.Single(updatedTransactions);
        Assert.Equal(80m, recalculatedTransaction.TotalAmountPaid);
        Assert.Equal(100m, recalculatedTransaction.TotalPayable);
        Assert.False(recalculatedTransaction.IsCompletelyPaid);
        Assert.Equal(TransactionStatus.PartiallyPaid, recalculatedTransaction.TransactionStatus);
    }

    [Fact]
    public async Task AddPayment_Debit_DeductsFromTotalAmountPaid()
    {
        var studentTransactionId = Guid.Parse("10101010-2020-3030-4040-505050505050");
        var familyId = Guid.Parse("60606060-7070-8080-9090-a0a0a0a0a0a0");
        var updatedTransactions = new List<AddStudentCourseTransaction>();

        var repository = new Mock<ICoursePaymentRepository>();
        repository
            .Setup(repo => repo.TryAddPayment(It.IsAny<AddCoursePayment>()))
            .ReturnsAsync((new CoursePaymentResponse
            {
                PaymentId = Guid.NewGuid(),
                StudentCourseTransactionId = studentTransactionId,
                FamilyId = familyId,
                AmountPaid = 10m,
                PaymentType = PaymentType.Debit,
                PaymentMode = PaymentMode.CashOnCounter,
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
                    AmountPaid = 100m,
                    PaymentType = PaymentType.Credit,
                    PaymentMode = PaymentMode.CashOnCounter,
                    IsActive = true
                },
                new CoursePaymentResponse
                {
                    StudentCourseTransactionId = studentTransactionId,
                    FamilyId = familyId,
                    AmountPaid = 10m,
                    PaymentType = PaymentType.Debit,
                    PaymentMode = PaymentMode.CashOnCounter,
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
                PaymentCode = "DEBIT1",
                Comments = "existing",
                FeeInstallments = new List<FeeInstallment>(),
                RegistrationStatus = RegistrationStatus.Pending,
                TransactionStatus = TransactionStatus.FullyPaid,
                IsActive = true,
                TotalPayable = 100m,
                TotalAmountPaid = 100m
            });
        studentCourseTransactionService
            .Setup(service => service.UpdateTransaction(studentTransactionId, It.IsAny<AddStudentCourseTransaction>()))
            .Callback<Guid, AddStudentCourseTransaction>((_, transaction) => updatedTransactions.Add(transaction))
            .ReturnsAsync(true);

        var service = new CoursePaymentService(
            repository.Object,
            studentCourseTransactionService.Object,
            Mock.Of<IStudentCourseEnrollmentService>());

        await service.AddPayment(new AddCoursePayment
        {
            StudentCourseTransactionId = studentTransactionId,
            FamilyId = familyId,
            AmountPaid = 10m,
            Comments = "debit adjustment",
            PaymentMode = PaymentMode.CashOnCounter,
            PaymentType = PaymentType.Debit,
            IsActive = true
        });

        var updated = Assert.Single(updatedTransactions);
        Assert.Equal(90m, updated.TotalAmountPaid);
        Assert.Equal(100m, updated.TotalPayable);
        Assert.False(updated.IsCompletelyPaid);
        Assert.Equal(TransactionStatus.PartiallyPaid, updated.TransactionStatus);
    }

    [Fact]
    public async Task AddPayment_PreservesExistingTransactionSurcharge()
    {
        var studentTransactionId = Guid.Parse("efefefef-efef-efef-efef-efefefefefef");
        var familyId = Guid.Parse("01010101-0101-0101-0101-010101010101");
        var updatedTransactions = new List<AddStudentCourseTransaction>();

        var repository = new Mock<ICoursePaymentRepository>();
        repository
            .Setup(repo => repo.TryAddPayment(It.IsAny<AddCoursePayment>()))
            .ReturnsAsync((new CoursePaymentResponse
            {
                PaymentId = Guid.NewGuid(),
                StudentCourseTransactionId = studentTransactionId,
                FamilyId = familyId,
                AmountPaid = 5m,
                PaymentType = PaymentType.Surcharge,
                PaymentMode = PaymentMode.CashOnCounter,
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
                    AmountPaid = 100m,
                    PaymentType = PaymentType.Credit,
                    PaymentMode = PaymentMode.CashOnCounter,
                    IsActive = true
                },
                new CoursePaymentResponse
                {
                    StudentCourseTransactionId = studentTransactionId,
                    FamilyId = familyId,
                    AmountPaid = 5m,
                    PaymentType = PaymentType.Surcharge,
                    PaymentMode = PaymentMode.CashOnCounter,
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
                PaymentCode = "SURCHARGE1",
                Comments = "existing",
                FeeInstallments = new List<FeeInstallment>(),
                RegistrationStatus = RegistrationStatus.Pending,
                TransactionStatus = TransactionStatus.FullyPaid,
                IsActive = true,
                TotalPayable = 105m,
                TotalAmountPaid = 100m
                ,
                Surcharge = 5d
            });
        studentCourseTransactionService
            .Setup(service => service.UpdateTransaction(studentTransactionId, It.IsAny<AddStudentCourseTransaction>()))
            .Callback<Guid, AddStudentCourseTransaction>((_, transaction) => updatedTransactions.Add(transaction))
            .ReturnsAsync(true);

        var service = new CoursePaymentService(
            repository.Object,
            studentCourseTransactionService.Object,
            Mock.Of<IStudentCourseEnrollmentService>());

        await service.AddPayment(new AddCoursePayment
        {
            StudentCourseTransactionId = studentTransactionId,
            FamilyId = familyId,
            AmountPaid = 5m,
            Comments = "surcharge",
            PaymentMode = PaymentMode.CashOnCounter,
            PaymentType = PaymentType.Surcharge,
            IsActive = true
        });

        var updated = Assert.Single(updatedTransactions);
        Assert.Equal(100m, updated.TotalAmountPaid);
        Assert.Equal(105m, updated.TotalPayable);
        Assert.Equal(5d, updated.Surcharge);
        Assert.False(updated.IsCompletelyPaid);
        Assert.Equal(TransactionStatus.PartiallyPaid, updated.TransactionStatus);
    }
}
