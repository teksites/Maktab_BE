using Courses.Implementation.Services;
using Courses.Repository;
using Courses.Services;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Requests.Policies;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Responses.Institute;
using MaktabDataContracts.Responses.Transactions;
using MaktabDataContracts.Responses.Users;
using Moq;
using Newtonsoft.Json;
using Email;
using Users.Services;

namespace Courses.Test;

public class StudentCourseEnrollmentServiceTests
{
    [Fact]
    public async Task AddEnrollment_SetsEnrollmentToAwaitingWhenGroupIsAlreadyFull()
    {
        var courseId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var capturedEnrollment = default(AddStudentCourseEnrollment);
        var repository = new Mock<IStudentCourseEnrollmentRepository>();
        repository
            .Setup(repo => repo.GetCourseEnrollmentGroupInformation(groupId))
            .ReturnsAsync(new CourseEnrollmentGroupInformationResponse
            {
                CourseEnrollmentGroupId = groupId,
                CourseId = courseId,
                MaxStudents = 2,
                IfRegistrationOpen = true,
                EnrollmentStatusCount = new Dictionary<EnrollmentStatus, int>
                {
                    [EnrollmentStatus.Enrolled] = 1,
                    [EnrollmentStatus.Registered] = 1
                }
            });
        repository
            .Setup(repo => repo.AddEnrollment(It.IsAny<AddStudentCourseEnrollment>()))
            .Callback<AddStudentCourseEnrollment>(enrollment => capturedEnrollment = enrollment)
            .ReturnsAsync(() => new StudentCourseEnrollmentResponse
            {
                StudentCourseEnrollmentId = Guid.NewGuid(),
                ChildId = childId,
                CourseEnrollmentGroupId = groupId,
                CourseId = courseId,
                FamilyId = familyId,
                EnrollmentStatus = capturedEnrollment!.EnrollmentStatus,
                CreatedAt = DateTime.UtcNow,
                UpdatedOn = DateTime.UtcNow
            });

        var transactionService = new Mock<IStudentCourseTransactionService>();
        transactionService
            .Setup(service => service.GetCourseTransactionsByFamily(courseId, familyId))
            .ReturnsAsync(Array.Empty<StudentCourseTransactionResponse>());
        transactionService
            .Setup(service => service.AddTransaction(It.IsAny<AddStudentCourseTransaction>()))
            .ReturnsAsync(new StudentCourseTransactionResponse
            {
                StudentCourseTransactionId = Guid.NewGuid(),
                FamilyId = familyId
            });
        transactionService
            .Setup(service => service.AddEnrollmentsToTransaction(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(true);

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetCourse(courseId))
            .ReturnsAsync(CreateCourse(courseId, groupId, 120));

        var policyService = new Mock<IInstitutePolicyService>();
        policyService
            .Setup(service => service.GetAllPolicies(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<InstitutePolicyResponse>());

        var groupService = new Mock<ICourseEnrollmentGroupService>();
        groupService
            .Setup(service => service.SetCourseGroupRegistrationStatus(groupId, false))
            .ReturnsAsync(new CourseEnrollmentGroupResponse
            {
                CourseEnrollmentGroupId = groupId,
                CourseId = courseId,
                IfRegistrationOpen = false
            });

        var service = CreateEnrollmentService(
            repository: repository,
            transactionService: transactionService,
            courseService: courseService,
            policyService: policyService,
            groupService: groupService);

        var response = await service.AddEnrollment(new AddStudentCourseEnrollment
        {
            ChildId = childId,
            FamilyId = familyId,
            CourseId = courseId,
            CourseEnrollmentGroupId = groupId,
            WillUseDayCare = false,
            DayCareDays = 0
        });

        Assert.NotNull(capturedEnrollment);
        Assert.Equal(EnrollmentStatus.Awaiting, capturedEnrollment!.EnrollmentStatus);
        Assert.Equal(EnrollmentStatus.Awaiting, response.EnrollmentStatus);
    }

    [Fact]
    public async Task RecalculateCourseFee_UsesDefaultSingleInstallmentWhenPolicyFallbackApplies()
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
                    CourseId = courseId,
                    Details = JsonConvert.SerializeObject(new[]
                    {
                        new FeePaymentPolicy
                        {
                            Name = "Second by date",
                            PaymentDate = DateTime.UtcNow.Date.AddDays(14),
                            PercentageToCover = 40,
                            MinimalChildrenToApply = 1,
                            ShouldApplyEnrollmentToCover = false
                        },
                        new FeePaymentPolicy
                        {
                            Name = "First by date",
                            PaymentDate = DateTime.UtcNow.Date.AddDays(7),
                            PercentageToCover = 40,
                            MinimalChildrenToApply = 1,
                            ShouldApplyEnrollmentToCover = false
                        }
                    }),
                    InstutePolicy = PolicyType.CourseFeePayment,
                    IsActive = true
                }
            });

        var service = CreateEnrollmentService(
            transactionService: transactionService,
            courseService: courseService,
            policyService: policyService);

        var result = await service.RecalculateCourseFee(courseId, familyId);

        Assert.True(result);
        Assert.NotNull(capturedUpdate);
        var installment = Assert.Single(capturedUpdate!.FeeInstallments);
        Assert.Equal("Paiement complet de l'inscription/Complete Registration Payment", installment.Description);
        Assert.Equal(120m, installment.Amount);
    }

    [Fact]
    public async Task RecalculateCourseFee_CreatesEnrollmentDrivenInstallmentsForSingleChildWithThreeGroups()
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
                    CourseId = courseId,
                    Details = JsonConvert.SerializeObject(new[]
                    {
                        new FeePaymentPolicy
                        {
                            Name = "First installment",
                            PaymentDate = DateTime.UtcNow.Date.AddDays(7),
                            EnrollmentsToCover = 1,
                            ShouldApplyEnrollmentToCover = true
                        },
                        new FeePaymentPolicy
                        {
                            Name = "Second installment",
                            PaymentDate = DateTime.UtcNow.Date.AddDays(14),
                            EnrollmentsToCover = 1,
                            ShouldApplyEnrollmentToCover = true
                        },
                        new FeePaymentPolicy
                        {
                            Name = "Third installment",
                            PaymentDate = DateTime.UtcNow.Date.AddDays(21),
                            EnrollmentsToCover = 1,
                            ShouldApplyEnrollmentToCover = true
                        }
                    }),
                    InstutePolicy = PolicyType.CourseFeePayment,
                    IsActive = true
                }
            });

        var service = CreateEnrollmentService(
            transactionService: transactionService,
            courseService: courseService,
            policyService: policyService);

        var result = await service.RecalculateCourseFee(courseId, familyId);

        Assert.True(result);
        Assert.NotNull(capturedUpdate);
        Assert.Equal(300m, capturedUpdate!.TotalPayable);
        Assert.Equal(3, capturedUpdate.FeeInstallments.Count);
        Assert.Equal("First installment", capturedUpdate.FeeInstallments[0].Description);
        Assert.Equal("Second installment", capturedUpdate.FeeInstallments[1].Description);
        Assert.Equal("Third installment", capturedUpdate.FeeInstallments[2].Description);
        Assert.Equal(300m, capturedUpdate.FeeInstallments.Sum(installment => installment.Amount));
        Assert.Equal(100m, capturedUpdate.FeeInstallments[0].Amount);
        Assert.Equal(100m, capturedUpdate.FeeInstallments[1].Amount);
        Assert.Equal(100m, capturedUpdate.FeeInstallments[2].Amount);
    }

    [Fact]
    public async Task RecalculateCourseFee_StopsEnrollmentDrivenInstallmentsAfterLastApplicablePolicyAndAddsRegistrationFeeToFirst()
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
            .ReturnsAsync(CreateCourse(courseId, groupIds[0], 100, true, 50, true, groupIds[1], groupIds[2]));

        var policyService = new Mock<IInstitutePolicyService>();
        policyService
            .Setup(service => service.GetAllPolicies(It.IsAny<Guid>()))
            .ReturnsAsync(new[]
            {
                new InstitutePolicyResponse
                {
                    InstitutePolicyId = Guid.NewGuid(),
                    InstituteId = Guid.NewGuid(),
                    CourseId = courseId,
                    Details = JsonConvert.SerializeObject(new[]
                    {
                        new FeePaymentPolicy
                        {
                            Name = "First installment",
                            PaymentDate = DateTime.UtcNow.Date.AddDays(7),
                            EnrollmentsToCover = 2,
                            ShouldApplyEnrollmentToCover = true
                        },
                        new FeePaymentPolicy
                        {
                            Name = "Second installment",
                            PaymentDate = DateTime.UtcNow.Date.AddDays(14),
                            EnrollmentsToCover = 2,
                            ShouldApplyEnrollmentToCover = true
                        },
                        new FeePaymentPolicy
                        {
                            Name = "Third installment",
                            PaymentDate = DateTime.UtcNow.Date.AddDays(21),
                            EnrollmentsToCover = 2,
                            ShouldApplyEnrollmentToCover = true
                        },
                        new FeePaymentPolicy
                        {
                            Name = "Fourth installment",
                            PaymentDate = DateTime.UtcNow.Date.AddDays(28),
                            EnrollmentsToCover = 2,
                            ShouldApplyEnrollmentToCover = true
                        },
                        new FeePaymentPolicy
                        {
                            Name = "Fifth installment",
                            PaymentDate = DateTime.UtcNow.Date.AddDays(35),
                            EnrollmentsToCover = 2,
                            ShouldApplyEnrollmentToCover = true
                        }
                    }),
                    InstutePolicy = PolicyType.CourseFeePayment,
                    IsActive = true
                }
            });

        var service = CreateEnrollmentService(
            transactionService: transactionService,
            courseService: courseService,
            policyService: policyService);

        var result = await service.RecalculateCourseFee(courseId, familyId);

        Assert.True(result);
        Assert.NotNull(capturedUpdate);
        Assert.Equal(350m, capturedUpdate!.TotalPayable);
        Assert.Equal(2, capturedUpdate.FeeInstallments.Count);
        Assert.Equal("First installment", capturedUpdate.FeeInstallments[0].Description);
        Assert.Equal("Second installment", capturedUpdate.FeeInstallments[1].Description);
        Assert.Equal(350m, capturedUpdate.FeeInstallments.Sum(installment => installment.Amount));
        Assert.Equal(250m, capturedUpdate.FeeInstallments[0].Amount);
        Assert.Equal(100m, capturedUpdate.FeeInstallments[1].Amount);
    }

    [Fact]
    public async Task UpdateEnrollment_ReopensClosedGroupWhenConfirmedEnrollmentIsCancelled()
    {
        var enrollmentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var repository = new Mock<IStudentCourseEnrollmentRepository>();
        repository
            .Setup(repo => repo.GetEnrollment(enrollmentId))
            .ReturnsAsync(CreateEnrollment(childId, groupId, courseId, familyId, EnrollmentStatus.Registered));
        repository
            .Setup(repo => repo.UpdateEnrollment(enrollmentId, It.IsAny<AddStudentCourseEnrollment>()))
            .ReturnsAsync(true);

        var transactionService = new Mock<IStudentCourseTransactionService>();
        transactionService
            .Setup(service => service.GetCourseTransactionsByFamily(courseId, familyId))
            .ReturnsAsync(new[]
            {
                CreateFamilyTransaction(courseId, familyId, new[]
                {
                    CreateEnrollment(childId, groupId, courseId, familyId, EnrollmentStatus.Registered)
                })
            });
        transactionService
            .Setup(service => service.UpdateTransaction(It.IsAny<Guid>(), It.IsAny<AddStudentCourseTransaction>()))
            .ReturnsAsync(true);

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetCourse(courseId))
            .ReturnsAsync(CreateCourse(courseId, groupId, 100, ifRegistrationOpen: false));

        var policyService = new Mock<IInstitutePolicyService>();
        policyService
            .Setup(service => service.GetAllPolicies(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<InstitutePolicyResponse>());

        var groupService = new Mock<ICourseEnrollmentGroupService>();
        groupService
            .Setup(service => service.SetCourseGroupRegistrationStatus(groupId, true))
            .ReturnsAsync(new CourseEnrollmentGroupResponse
            {
                CourseEnrollmentGroupId = groupId,
                CourseId = courseId,
                IfRegistrationOpen = true
            });

        var userService = new Mock<IUserService>();
        userService
            .Setup(service => service.GetAllFamilyUsersInformation(familyId, true))
            .ReturnsAsync(Array.Empty<UserInformationResponse>());

        var service = CreateEnrollmentService(
            repository: repository,
            transactionService: transactionService,
            courseService: courseService,
            policyService: policyService,
            groupService: groupService,
            userService: userService);

        var result = await service.UpdateEnrollment(enrollmentId, new AddStudentCourseEnrollment
        {
            ChildId = childId,
            FamilyId = familyId,
            CourseId = courseId,
            CourseEnrollmentGroupId = groupId,
            EnrollmentStatus = EnrollmentStatus.Cancelled,
            WillUseDayCare = false,
            DayCareDays = 0
        }, ifUpdatedByAdmin: true);

        Assert.True(result);
        groupService.Verify(service => service.SetCourseGroupRegistrationStatus(groupId, true), Times.Once);
    }

    [Fact]
    public async Task UpdateEnrollment_DoesNotAllowMoveIntoSeatHoldingStatusWhenGroupIsFull()
    {
        var enrollmentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var repository = new Mock<IStudentCourseEnrollmentRepository>();
        repository
            .Setup(repo => repo.GetEnrollment(enrollmentId))
            .ReturnsAsync(CreateEnrollment(childId, groupId, courseId, familyId, EnrollmentStatus.Awaiting));
        repository
            .Setup(repo => repo.GetCourseEnrollmentGroupInformation(groupId))
            .ReturnsAsync(new CourseEnrollmentGroupInformationResponse
            {
                CourseEnrollmentGroupId = groupId,
                CourseId = courseId,
                MaxStudents = 3,
                IfRegistrationOpen = true,
                EnrollmentStatusCount = new Dictionary<EnrollmentStatus, int>
                {
                    [EnrollmentStatus.Enrolled] = 2,
                    [EnrollmentStatus.Registered] = 1
                }
            });

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetCourse(courseId))
            .ReturnsAsync(CreateCourse(courseId, groupId, 100));

        var service = CreateEnrollmentService(
            repository: repository,
            courseService: courseService);

        var result = await service.UpdateEnrollment(enrollmentId, new AddStudentCourseEnrollment
        {
            ChildId = childId,
            FamilyId = familyId,
            CourseId = courseId,
            CourseEnrollmentGroupId = groupId,
            EnrollmentStatus = EnrollmentStatus.Enrolled,
            WillUseDayCare = false,
            DayCareDays = 0
        }, ifUpdatedByAdmin: true);

        Assert.False(result);
        repository.Verify(repo => repo.UpdateEnrollment(enrollmentId, It.IsAny<AddStudentCourseEnrollment>()), Times.Never);
    }

    [Fact]
    public async Task UpdateEnrollment_ClosesGroupWhenMoveIntoSeatHoldingStatusFillsLastSeat()
    {
        var enrollmentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var repository = new Mock<IStudentCourseEnrollmentRepository>();
        repository
            .Setup(repo => repo.GetEnrollment(enrollmentId))
            .ReturnsAsync(CreateEnrollment(childId, groupId, courseId, familyId, EnrollmentStatus.Awaiting));
        repository
            .SetupSequence(repo => repo.GetCourseEnrollmentGroupInformation(groupId))
            .ReturnsAsync(new CourseEnrollmentGroupInformationResponse
            {
                CourseEnrollmentGroupId = groupId,
                CourseId = courseId,
                MaxStudents = 3,
                IfRegistrationOpen = true,
                EnrollmentStatusCount = new Dictionary<EnrollmentStatus, int>
                {
                    [EnrollmentStatus.Enrolled] = 2,
                    [EnrollmentStatus.Registered] = 0
                }
            })
            .ReturnsAsync(new CourseEnrollmentGroupInformationResponse
            {
                CourseEnrollmentGroupId = groupId,
                CourseId = courseId,
                MaxStudents = 3,
                IfRegistrationOpen = true,
                EnrollmentStatusCount = new Dictionary<EnrollmentStatus, int>
                {
                    [EnrollmentStatus.Enrolled] = 3,
                    [EnrollmentStatus.Registered] = 0
                }
            });
        repository
            .Setup(repo => repo.UpdateEnrollment(enrollmentId, It.IsAny<AddStudentCourseEnrollment>()))
            .ReturnsAsync(true);

        var transactionService = new Mock<IStudentCourseTransactionService>();
        transactionService
            .Setup(service => service.GetCourseTransactionsByFamily(courseId, familyId))
            .ReturnsAsync(new[]
            {
                CreateFamilyTransaction(courseId, familyId, new[]
                {
                    CreateEnrollment(childId, groupId, courseId, familyId, EnrollmentStatus.Enrolled)
                })
            });
        transactionService
            .Setup(service => service.UpdateTransaction(It.IsAny<Guid>(), It.IsAny<AddStudentCourseTransaction>()))
            .ReturnsAsync(true);

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetCourse(courseId))
            .ReturnsAsync(CreateCourse(courseId, groupId, 100, ifRegistrationOpen: true));

        var policyService = new Mock<IInstitutePolicyService>();
        policyService
            .Setup(service => service.GetAllPolicies(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<InstitutePolicyResponse>());

        var groupService = new Mock<ICourseEnrollmentGroupService>();
        groupService
            .Setup(service => service.SetCourseGroupRegistrationStatus(groupId, false))
            .ReturnsAsync(new CourseEnrollmentGroupResponse
            {
                CourseEnrollmentGroupId = groupId,
                CourseId = courseId,
                IfRegistrationOpen = false
            });

        var userService = new Mock<IUserService>();
        userService
            .Setup(service => service.GetAllFamilyUsersInformation(familyId, true))
            .ReturnsAsync(Array.Empty<UserInformationResponse>());

        var service = CreateEnrollmentService(
            repository: repository,
            transactionService: transactionService,
            courseService: courseService,
            policyService: policyService,
            groupService: groupService,
            userService: userService);

        var result = await service.UpdateEnrollment(enrollmentId, new AddStudentCourseEnrollment
        {
            ChildId = childId,
            FamilyId = familyId,
            CourseId = courseId,
            CourseEnrollmentGroupId = groupId,
            EnrollmentStatus = EnrollmentStatus.Enrolled,
            WillUseDayCare = false,
            DayCareDays = 0
        }, ifUpdatedByAdmin: true);

        Assert.True(result);
        groupService.Verify(service => service.SetCourseGroupRegistrationStatus(groupId, false), Times.Once);
    }

    [Fact]
    public async Task DeleteEnrollment_ReopensClosedGroupWhenConfirmedEnrollmentIsRemoved()
    {
        var enrollmentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var repository = new Mock<IStudentCourseEnrollmentRepository>();
        repository
            .Setup(repo => repo.GetEnrollment(enrollmentId))
            .ReturnsAsync(CreateEnrollment(childId, groupId, courseId, familyId, EnrollmentStatus.Enrolled));
        repository
            .Setup(repo => repo.DeleteEnrollment(enrollmentId, false))
            .ReturnsAsync(true);

        var transactionService = new Mock<IStudentCourseTransactionService>();
        var transactionId = Guid.NewGuid();
        transactionService
            .Setup(service => service.GetCourseTransactionsByFamily(courseId, familyId))
            .ReturnsAsync(new[]
            {
                new StudentCourseTransactionResponse
                {
                    StudentCourseTransactionId = transactionId,
                    FamilyId = familyId,
                    TotalAmountPaid = 0m,
                    Enrollments = new List<StudentCourseEnrollmentResponse>
                    {
                        CreateEnrollment(childId, groupId, courseId, familyId, EnrollmentStatus.Enrolled)
                    }
                }
            });
        transactionService
            .Setup(service => service.DeleteStudentCourseTransactionEnrollmentByEnrollmentId(enrollmentId))
            .ReturnsAsync(true);
        transactionService
            .Setup(service => service.DeleteTransaction(transactionId, true))
            .ReturnsAsync(true);

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetCourse(courseId))
            .ReturnsAsync(CreateCourse(courseId, groupId, 100, ifRegistrationOpen: false, courseIsRegistrationOpen: true));

        var groupService = new Mock<ICourseEnrollmentGroupService>();
        groupService
            .Setup(service => service.SetCourseGroupRegistrationStatus(groupId, true))
            .ReturnsAsync(new CourseEnrollmentGroupResponse
            {
                CourseEnrollmentGroupId = groupId,
                CourseId = courseId,
                IfRegistrationOpen = true
            });

        var service = CreateEnrollmentService(
            repository: repository,
            transactionService: transactionService,
            courseService: courseService,
            groupService: groupService);

        var result = await service.DeleteEnrollment(enrollmentId, hardDelete: false, ifDeletedByAdmin: false);

        Assert.True(result);
        groupService.Verify(service => service.SetCourseGroupRegistrationStatus(groupId, true), Times.Once);
        transactionService.Verify(service => service.DeleteTransaction(transactionId, true), Times.Once);
    }

    private static StudentCourseEnrollmentService CreateEnrollmentService(
        Mock<IStudentCourseEnrollmentRepository>? repository = null,
        Mock<IStudentCourseTransactionService>? transactionService = null,
        Mock<ICourseService>? courseService = null,
        Mock<IInstitutePolicyService>? policyService = null,
        Mock<ICourseEnrollmentGroupService>? groupService = null,
        Mock<IUserService>? userService = null)
    {
        return new StudentCourseEnrollmentService(
            (repository ?? new Mock<IStudentCourseEnrollmentRepository>()).Object,
            (transactionService ?? new Mock<IStudentCourseTransactionService>()).Object,
            (courseService ?? new Mock<ICourseService>()).Object,
            (policyService ?? new Mock<IInstitutePolicyService>()).Object,
            (groupService ?? new Mock<ICourseEnrollmentGroupService>()).Object,
            Mock.Of<ISendEmailService>(),
            (userService ?? new Mock<IUserService>()).Object);
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

    private static StudentCourseEnrollmentResponse CreateEnrollment(
        Guid childId,
        Guid groupId,
        Guid courseId,
        Guid? familyId = null,
        EnrollmentStatus status = EnrollmentStatus.Registered)
    {
        return new StudentCourseEnrollmentResponse
        {
            StudentCourseEnrollmentId = Guid.NewGuid(),
            ChildId = childId,
            CourseEnrollmentGroupId = groupId,
            CourseId = courseId,
            FamilyId = familyId ?? Guid.NewGuid(),
            EnrollmentStatus = status,
            WillUseDayCare = false,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            UpdatedOn = DateTime.UtcNow
        };
    }

    private static CourseResponseDetailed CreateCourse(Guid courseId, Guid firstGroupId, int fee, params Guid[] otherGroupIds)
        => CreateCourse(courseId, firstGroupId, fee, true, 0, true, otherGroupIds);

    private static CourseResponseDetailed CreateCourse(
        Guid courseId,
        Guid firstGroupId,
        int fee,
        bool ifRegistrationOpen,
        bool courseIsRegistrationOpen = true,
        params Guid[] otherGroupIds)
        => CreateCourse(courseId, firstGroupId, fee, ifRegistrationOpen, 0, courseIsRegistrationOpen, otherGroupIds);

    private static CourseResponseDetailed CreateCourse(
        Guid courseId,
        Guid firstGroupId,
        int fee,
        bool ifRegistrationOpen,
        int registrationFee,
        bool courseIsRegistrationOpen = true,
        params Guid[] otherGroupIds)
    {
        var groupIds = new[] { firstGroupId }.Concat(otherGroupIds).ToList();

        return new CourseResponseDetailed
        {
            CourseId = courseId,
            InstituteId = Guid.NewGuid(),
            RegistrationFee = registrationFee,
            IsRegistrationOpened = courseIsRegistrationOpen,
            CanSelectMultipleEnrollmentGroups = groupIds.Count > 1,
            CourseEnrollmentGroups = groupIds.Select(groupId => new CourseEnrollmentGroupResponse
            {
                CourseEnrollmentGroupId = groupId,
                CourseId = courseId,
                InstituteId = Guid.NewGuid(),
                Fee = fee,
                DayCareFee = 0,
                IfRegistrationOpen = ifRegistrationOpen,
                GroupTitle = "Group",
                GroupTitleFr = "Groupe",
                AcedemicGroups = new List<string>()
            }).ToList()
        };
    }
}
