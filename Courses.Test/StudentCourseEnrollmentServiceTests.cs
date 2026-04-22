using Courses.Implementation.Services;
using Courses.Repository;
using Courses.Services;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Requests.Policies;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Responses.Institute;
using MaktabDataContracts.Responses.Transactions;
using Moq;
using Newtonsoft.Json;

namespace Courses.Test;

public class StudentCourseEnrollmentServiceTests
{
    [Fact]
    public async Task RecalculateCourseFee_UsesFallbackSingleInstallmentWhenFeePolicyIsInvalid()
    {
        var courseId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var capturedUpdate = default(AddStudentCourseTransaction);

        var transactionService = new Mock<IStudentCourseTransactionService>();
        transactionService
            .Setup(service => service.GetCourseTransactionsByFamily(courseId, familyId))
            .ReturnsAsync(new[]
            {
                CreateFamilyTransaction(courseId, familyId, new[]
                {
                    CreateEnrollment(childId, groupId, courseId)
                })
            });
        transactionService
            .Setup(service => service.UpdateTransaction(It.IsAny<Guid>(), It.IsAny<AddStudentCourseTransaction>()))
            .Callback<Guid, AddStudentCourseTransaction>((_, transaction) => capturedUpdate = transaction)
            .ReturnsAsync(true);

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetCourse(courseId))
            .ReturnsAsync(CreateCourse(courseId, groupId, 120));

        var policyService = new Mock<IInstitutePolicyService>();
        policyService
            .Setup(service => service.GetAllPolicies(It.IsAny<Guid>()))
            .ReturnsAsync(new[]
            {
                new InstitutePolicyResponse
                {
                    InstitutePolicyId = Guid.NewGuid(),
                    InstituteId = Guid.NewGuid(),
                    Details = JsonConvert.SerializeObject(new[]
                    {
                        new FeePaymentPolicy
                        {
                            Name = "Installment 1",
                            PaymentDate = DateTime.UtcNow.Date.AddDays(7),
                            MinimumAmountDue = 40
                        },
                        new FeePaymentPolicy
                        {
                            Name = "Installment 2",
                            PaymentDate = DateTime.UtcNow.Date.AddDays(14),
                            MinimumAmountDue = 40
                        }
                    }),
                    InstutePolicy = PolicyType.CourseFeePayment,
                    IsActive = true
                }
            });

        var service = CreateEnrollmentService(transactionService, courseService, policyService);

        var result = await service.RecalculateCourseFee(courseId, familyId);

        Assert.True(result);
        Assert.NotNull(capturedUpdate);
        var installment = Assert.Single(capturedUpdate!.FeeInstallments);
        Assert.Equal("Paiement complet de l'inscription/Complete Registration Payment", installment.Description);
        Assert.Equal(120m, installment.Amount);
    }

    [Fact]
    public async Task RecalculateCourseFee_CreatesRuleBasedInstallmentsForSingleChildWithThreeGroups()
    {
        var courseId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var groupIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var capturedUpdate = default(AddStudentCourseTransaction);

        var transactionService = new Mock<IStudentCourseTransactionService>();
        transactionService
            .Setup(service => service.GetCourseTransactionsByFamily(courseId, familyId))
            .ReturnsAsync(new[]
            {
                CreateFamilyTransaction(courseId, familyId, new[]
                {
                    CreateEnrollment(childId, groupIds[0], courseId),
                    CreateEnrollment(childId, groupIds[1], courseId),
                    CreateEnrollment(childId, groupIds[2], courseId)
                })
            });
        transactionService
            .Setup(service => service.UpdateTransaction(It.IsAny<Guid>(), It.IsAny<AddStudentCourseTransaction>()))
            .Callback<Guid, AddStudentCourseTransaction>((_, transaction) => capturedUpdate = transaction)
            .ReturnsAsync(true);

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetCourse(courseId))
            .ReturnsAsync(CreateCourse(courseId, groupIds[0], 100, groupIds[1], groupIds[2]));

        var policyService = new Mock<IInstitutePolicyService>();
        policyService
            .Setup(service => service.GetAllPolicies(It.IsAny<Guid>()))
            .ReturnsAsync(new[]
            {
                new InstitutePolicyResponse
                {
                    InstitutePolicyId = Guid.NewGuid(),
                    InstituteId = Guid.NewGuid(),
                    Details = JsonConvert.SerializeObject(new[]
                    {
                        new FeePaymentPolicy
                        {
                            Name = "First installment",
                            PaymentDate = DateTime.UtcNow.Date.AddDays(7),
                            MinimumAmountDue = 40
                        },
                        new FeePaymentPolicy
                        {
                            Name = "Second installment",
                            PaymentDate = DateTime.UtcNow.Date.AddDays(14),
                            MinimumAmountDue = 30
                        },
                        new FeePaymentPolicy
                        {
                            Name = "Third installment",
                            PaymentDate = DateTime.UtcNow.Date.AddDays(21),
                            MinimumAmountDue = 30
                        }
                    }),
                    InstutePolicy = PolicyType.CourseFeePayment,
                    IsActive = true
                }
            });

        var service = CreateEnrollmentService(transactionService, courseService, policyService);

        var result = await service.RecalculateCourseFee(courseId, familyId);

        Assert.True(result);
        Assert.NotNull(capturedUpdate);
        Assert.Equal(300m, capturedUpdate!.TotalPayable);
        Assert.Equal(2, capturedUpdate.FeeInstallments.Count);
        Assert.Equal("First installment", capturedUpdate.FeeInstallments[0].Description);
        Assert.Equal("Second installment", capturedUpdate.FeeInstallments[1].Description);
        Assert.Equal(300m, capturedUpdate.FeeInstallments.Sum(installment => installment.Amount));
        Assert.Equal(150m, capturedUpdate.FeeInstallments[0].Amount);
        Assert.Equal(150m, capturedUpdate.FeeInstallments[1].Amount);
    }

    private static StudentCourseEnrollmentService CreateEnrollmentService(
        Mock<IStudentCourseTransactionService> transactionService,
        Mock<ICourseService> courseService,
        Mock<IInstitutePolicyService> policyService)
    {
        return new StudentCourseEnrollmentService(
            Mock.Of<IStudentCourseEnrollmentRepository>(),
            transactionService.Object,
            courseService.Object,
            policyService.Object,
            Mock.Of<ICourseEnrollmentGroupService>());
    }

    private static StudentCourseTransactionResponse CreateFamilyTransaction(
        Guid courseId,
        Guid familyId,
        IEnumerable<StudentCourseEnrollmentResponse> enrollments)
    {
        return new StudentCourseTransactionResponse
        {
            StudentCourseTransactionId = Guid.NewGuid(),
            FamilyId = familyId,
            PaymentCode = "PAY001",
            TransactionStatus = TransactionStatus.AwaitingPayment,
            RegistrationStatus = RegistrationStatus.Pending,
            IsActive = true,
            TotalAmountPaid = 0m,
            FeeAmountDiscount = 0m,
            DayCareDiscount = 0m,
            Enrollments = enrollments.ToList()
        };
    }

    private static StudentCourseEnrollmentResponse CreateEnrollment(Guid childId, Guid groupId, Guid courseId)
    {
        return new StudentCourseEnrollmentResponse
        {
            StudentCourseEnrollmentId = Guid.NewGuid(),
            ChildId = childId,
            CourseEnrollmentGroupId = groupId,
            CourseId = courseId,
            FamilyId = Guid.NewGuid(),
            EnrollmentStatus = EnrollmentStatus.Registered,
            WillUseDayCare = false,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            UpdatedOn = DateTime.UtcNow
        };
    }

    private static CourseResponseDetailed CreateCourse(Guid courseId, Guid firstGroupId, int fee, params Guid[] otherGroupIds)
    {
        var groupIds = new[] { firstGroupId }.Concat(otherGroupIds).ToList();

        return new CourseResponseDetailed
        {
            CourseId = courseId,
            InstituteId = Guid.NewGuid(),
            RegistrationFee = 0,
            IsRegistrationOpened = true,
            CourseEnrollmentGroups = groupIds.Select(groupId => new CourseEnrollmentGroupResponse
            {
                CourseEnrollmentGroupId = groupId,
                CourseId = courseId,
                InstituteId = Guid.NewGuid(),
                Fee = fee,
                DayCareFee = 0,
                IfRegistrationOpen = true,
                GroupTitle = "Group",
                GroupTitleFr = "Groupe",
                AcedemicGroups = new List<string>()
            }).ToList()
        };
    }
}
