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
    public async Task AddEnrollment_WhenCourseIsManualEnrollment_SetsAwaitingAndSendsManualEmail()
    {
        var courseId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        AddStudentCourseEnrollment? capturedEnrollment = null;
        MultiUserEmailData? sentEmail = null;

        var repository = new Mock<IStudentCourseEnrollmentRepository>();
        repository
            .Setup(repo => repo.GetCourseEnrollmentGroupInformation(groupId))
            .ReturnsAsync(new CourseEnrollmentGroupInformationResponse
            {
                CourseEnrollmentGroupId = groupId,
                CourseId = courseId,
                MaxStudents = 20,
                IfRegistrationOpen = true,
                EnrollmentStatusCount = new Dictionary<EnrollmentStatus, int>()
            });
        repository
            .Setup(repo => repo.AddEnrollment(It.IsAny<AddStudentCourseEnrollment>()))
            .Callback<AddStudentCourseEnrollment>(enrollment => capturedEnrollment = enrollment)
            .ReturnsAsync(() => new StudentCourseEnrollmentResponse
            {
                StudentCourseEnrollmentId = Guid.NewGuid(),
                ChildId = childId,
                ChildName = "Maryam",
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
            .ReturnsAsync(CreateCourse(courseId, groupId, 120, ifRegistrationOpen: true, registrationFee: 0, courseIsRegistrationOpen: true, isManualEnrollment: true));

        var policyService = new Mock<IInstitutePolicyService>();
        policyService
            .Setup(service => service.GetAllPolicies(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<InstitutePolicyResponse>());

        var groupService = new Mock<ICourseEnrollmentGroupService>();

        var userService = new Mock<IUserService>();
        userService
            .Setup(service => service.GetAllFamilyUsersInformation(familyId, true))
            .ReturnsAsync(new[]
            {
                new UserInformationResponse
                {
                    Email = "parent@example.com",
                    Relationship = Relationship.Mother,
                    IfTempUser = false
                },
                new UserInformationResponse
                {
                    Email = "parent2@example.com",
                    Relationship = Relationship.Father,
                    IfTempUser = false
                }
            });

        var sendEmailService = new Mock<ISendEmailService>();
        sendEmailService
            .Setup(service => service.SendBulkEmail(It.IsAny<MultiUserEmailData>()))
            .Callback<MultiUserEmailData>(email => sentEmail = email)
            .ReturnsAsync(true);

        var service = CreateEnrollmentService(
            repository: repository,
            transactionService: transactionService,
            courseService: courseService,
            policyService: policyService,
            groupService: groupService,
            userService: userService,
            sendEmailService: sendEmailService);

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
        Assert.NotNull(sentEmail);
        Assert.Equal("Confirmation de l'inscription / Confirmation of enrollment", sentEmail!.Subject);
        Assert.Contains("Maryam", sentEmail.Body);
        Assert.Contains("Summer Camp", sentEmail.Body);
        Assert.Contains("rattel.ecole@gmail.com", sentEmail.Body);
        groupService.Verify(
            service => service.SetCourseGroupRegistrationStatus(It.IsAny<Guid>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task AddEnrollment_WhenShouldTriggerEmailIsFalse_SkipsManualEnrollmentEmail()
    {
        var courseId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var repository = new Mock<IStudentCourseEnrollmentRepository>();
        repository
            .Setup(repo => repo.GetCourseEnrollmentGroupInformation(groupId))
            .ReturnsAsync(new CourseEnrollmentGroupInformationResponse
            {
                CourseEnrollmentGroupId = groupId,
                CourseId = courseId,
                MaxStudents = 20,
                IfRegistrationOpen = true,
                EnrollmentStatusCount = new Dictionary<EnrollmentStatus, int>()
            });
        repository
            .Setup(repo => repo.AddEnrollment(It.IsAny<AddStudentCourseEnrollment>()))
            .ReturnsAsync(new StudentCourseEnrollmentResponse
            {
                StudentCourseEnrollmentId = Guid.NewGuid(),
                ChildId = childId,
                ChildName = "Maryam",
                CourseEnrollmentGroupId = groupId,
                CourseId = courseId,
                FamilyId = familyId,
                EnrollmentStatus = EnrollmentStatus.Awaiting,
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
            .ReturnsAsync(CreateCourse(courseId, groupId, 120, ifRegistrationOpen: true, registrationFee: 0, courseIsRegistrationOpen: true, isManualEnrollment: true));

        var policyService = new Mock<IInstitutePolicyService>();
        policyService
            .Setup(service => service.GetAllPolicies(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<InstitutePolicyResponse>());

        var userService = new Mock<IUserService>();
        userService
            .Setup(service => service.GetAllFamilyUsersInformation(familyId, true))
            .ReturnsAsync(new[]
            {
                new UserInformationResponse
                {
                    Email = "parent@example.com",
                    Relationship = Relationship.Mother,
                    IfTempUser = false
                },
                new UserInformationResponse
                {
                    Email = "parent2@example.com",
                    Relationship = Relationship.Father,
                    IfTempUser = false
                }
            });

        var sendEmailService = new Mock<ISendEmailService>();

        var service = CreateEnrollmentService(
            repository: repository,
            transactionService: transactionService,
            courseService: courseService,
            policyService: policyService,
            userService: userService,
            sendEmailService: sendEmailService);

        var response = await service.AddEnrollment(new AddStudentCourseEnrollment
        {
            ChildId = childId,
            FamilyId = familyId,
            CourseId = courseId,
            CourseEnrollmentGroupId = groupId,
            WillUseDayCare = false,
            DayCareDays = 0,
            ShouldTriggerEmail = false
        });

        Assert.Equal(EnrollmentStatus.Awaiting, response.EnrollmentStatus);
        sendEmailService.Verify(service => service.SendBulkEmail(It.IsAny<MultiUserEmailData>()), Times.Never);
    }

    [Fact]
    public async Task AddEnrollment_WhenRegularCourseIsEnrolled_SendsStatusEmail()
    {
        var courseId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        AddStudentCourseEnrollment? capturedEnrollment = null;
        MultiUserEmailData? sentEmail = null;

        var repository = new Mock<IStudentCourseEnrollmentRepository>();
        repository
            .Setup(repo => repo.GetCourseEnrollmentGroupInformation(groupId))
            .ReturnsAsync(new CourseEnrollmentGroupInformationResponse
            {
                CourseEnrollmentGroupId = groupId,
                CourseId = courseId,
                MaxStudents = 20,
                IfRegistrationOpen = true,
                EnrollmentStatusCount = new Dictionary<EnrollmentStatus, int>()
            });
        repository
            .Setup(repo => repo.AddEnrollment(It.IsAny<AddStudentCourseEnrollment>()))
            .Callback<AddStudentCourseEnrollment>(enrollment => capturedEnrollment = enrollment)
            .ReturnsAsync(() => new StudentCourseEnrollmentResponse
            {
                StudentCourseEnrollmentId = Guid.NewGuid(),
                ChildId = childId,
                ChildName = "Maryam",
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
            .ReturnsAsync(CreateCourse(courseId, groupId, 120, ifRegistrationOpen: true, registrationFee: 0, courseIsRegistrationOpen: true, isManualEnrollment: false));

        var policyService = new Mock<IInstitutePolicyService>();
        policyService
            .Setup(service => service.GetAllPolicies(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<InstitutePolicyResponse>());

        var sendEmailService = new Mock<ISendEmailService>();
        sendEmailService
            .Setup(service => service.SendBulkEmail(It.IsAny<MultiUserEmailData>()))
            .Callback<MultiUserEmailData>(email => sentEmail = email)
            .ReturnsAsync(true);

        var service = CreateEnrollmentService(
            repository: repository,
            transactionService: transactionService,
            courseService: courseService,
            policyService: policyService,
            sendEmailService: sendEmailService);

        var response = await service.AddEnrollment(new AddStudentCourseEnrollment
        {
            ChildId = childId,
            FamilyId = familyId,
            CourseId = courseId,
            CourseEnrollmentGroupId = groupId,
            WillUseDayCare = false,
            DayCareDays = 0,
            ShouldTriggerEmail = true
        });

        Assert.Equal(EnrollmentStatus.Enrolled, response.EnrollmentStatus);
        Assert.NotNull(sentEmail);
        Assert.Equal("Confirmation de l'inscription / Confirmation of enrollment", sentEmail!.Subject);
        Assert.Contains("Summer Camp", sentEmail.Body);
        Assert.Contains("pay the fee", sentEmail.Body);
    }

    [Fact]
    public async Task AddEnrollment_WhenRegularCourseIsAwaiting_SendsWaitingListEmail()
    {
        var courseId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        AddStudentCourseEnrollment? capturedEnrollment = null;
        MultiUserEmailData? sentEmail = null;

        var repository = new Mock<IStudentCourseEnrollmentRepository>();
        repository
            .Setup(repo => repo.GetCourseEnrollmentGroupInformation(groupId))
            .ReturnsAsync(new CourseEnrollmentGroupInformationResponse
            {
                CourseEnrollmentGroupId = groupId,
                CourseId = courseId,
                MaxStudents = 1,
                IfRegistrationOpen = true,
                EnrollmentStatusCount = new Dictionary<EnrollmentStatus, int>
                {
                    [EnrollmentStatus.Enrolled] = 1
                }
            });
        repository
            .Setup(repo => repo.AddEnrollment(It.IsAny<AddStudentCourseEnrollment>()))
            .Callback<AddStudentCourseEnrollment>(enrollment => capturedEnrollment = enrollment)
            .ReturnsAsync(() => new StudentCourseEnrollmentResponse
            {
                StudentCourseEnrollmentId = Guid.NewGuid(),
                ChildId = childId,
                ChildName = "Maryam",
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
            .ReturnsAsync(CreateCourse(courseId, groupId, 120, ifRegistrationOpen: true, registrationFee: 0, courseIsRegistrationOpen: true, isManualEnrollment: false));

        var policyService = new Mock<IInstitutePolicyService>();
        policyService
            .Setup(service => service.GetAllPolicies(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<InstitutePolicyResponse>());

        var sendEmailService = new Mock<ISendEmailService>();
        sendEmailService
            .Setup(service => service.SendBulkEmail(It.IsAny<MultiUserEmailData>()))
            .Callback<MultiUserEmailData>(email => sentEmail = email)
            .ReturnsAsync(true);

        var service = CreateEnrollmentService(
            repository: repository,
            transactionService: transactionService,
            courseService: courseService,
            policyService: policyService,
            sendEmailService: sendEmailService);

        var response = await service.AddEnrollment(new AddStudentCourseEnrollment
        {
            ChildId = childId,
            FamilyId = familyId,
            CourseId = courseId,
            CourseEnrollmentGroupId = groupId,
            WillUseDayCare = false,
            DayCareDays = 0,
            ShouldTriggerEmail = true
        });

        Assert.Equal(EnrollmentStatus.Awaiting, response.EnrollmentStatus);
        Assert.NotNull(sentEmail);
        Assert.Contains("Waiting List", sentEmail!.Subject);
        Assert.Contains("Maryam", sentEmail.Body);
        Assert.Contains("waiting list", sentEmail.Body);
    }

    [Fact]
    public async Task AddEnrollment_WhenReAddingDifferentChild_DoesNotReuseExistingEnrollmentIndex()
    {
        var courseId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var returningChildId = Guid.NewGuid();
        var activeSiblingChildId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        AddStudentCourseEnrollment? capturedEnrollment = null;

        var repository = new Mock<IStudentCourseEnrollmentRepository>();
        repository
            .Setup(repo => repo.GetCourseEnrollmentGroupInformation(groupId))
            .ReturnsAsync(new CourseEnrollmentGroupInformationResponse
            {
                CourseEnrollmentGroupId = groupId,
                CourseId = courseId,
                MaxStudents = 10,
                IfRegistrationOpen = true,
                EnrollmentStatusCount = new Dictionary<EnrollmentStatus, int>()
            });
        repository
            .Setup(repo => repo.AddEnrollment(It.IsAny<AddStudentCourseEnrollment>()))
            .Callback<AddStudentCourseEnrollment>(enrollment => capturedEnrollment = enrollment)
            .ReturnsAsync(() => new StudentCourseEnrollmentResponse
            {
                StudentCourseEnrollmentId = Guid.NewGuid(),
                ChildId = returningChildId,
                CourseEnrollmentGroupId = groupId,
                CourseId = courseId,
                FamilyId = familyId,
                EnrollmentIndex = capturedEnrollment!.EnrollmentIndex,
                EnrollmentStatus = capturedEnrollment.EnrollmentStatus,
                CreatedAt = DateTime.UtcNow,
                UpdatedOn = DateTime.UtcNow
            });

        var existingActiveEnrollment = CreateEnrollment(
            activeSiblingChildId,
            groupId,
            courseId,
            familyId,
            EnrollmentStatus.Awaiting,
            "Existing Child",
            enrollmentIndex: 2);

        var transactionService = new Mock<IStudentCourseTransactionService>();
        transactionService
            .Setup(service => service.GetCourseTransactionsByFamily(courseId, familyId))
            .ReturnsAsync(new[]
            {
                CreateFamilyTransaction(courseId, familyId, new[] { existingActiveEnrollment })
            });
        transactionService
            .Setup(service => service.AddEnrollmentsToTransaction(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(true);
        transactionService
            .Setup(service => service.UpdateTransaction(It.IsAny<Guid>(), It.IsAny<AddStudentCourseTransaction>()))
            .ReturnsAsync(true);

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetCourse(courseId))
            .ReturnsAsync(CreateCourse(courseId, groupId, 120));

        var policyService = new Mock<IInstitutePolicyService>();
        policyService
            .Setup(service => service.GetAllPolicies(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<InstitutePolicyResponse>());

        var service = CreateEnrollmentService(
            repository: repository,
            transactionService: transactionService,
            courseService: courseService,
            policyService: policyService);

        var response = await service.AddEnrollment(new AddStudentCourseEnrollment
        {
            CourseId = courseId,
            FamilyId = familyId,
            ChildId = returningChildId,
            CourseEnrollmentGroupId = groupId,
            DayCareDays = 0,
            WillUseDayCare = false,
            ShouldTriggerEmail = false
        });

        Assert.NotNull(response);
        Assert.Equal(3, capturedEnrollment!.EnrollmentIndex);
    }

    [Fact]
    public async Task AddEnrollment_WhenRegularCourseMissingAParent_ThrowsAndDoesNotPersistEnrollment()
    {
        var courseId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var repository = new Mock<IStudentCourseEnrollmentRepository>();
        var transactionService = new Mock<IStudentCourseTransactionService>();
        transactionService
            .Setup(service => service.GetCourseTransactionsByFamily(courseId, familyId))
            .ReturnsAsync(Array.Empty<StudentCourseTransactionResponse>());

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetCourse(courseId))
            .ReturnsAsync(CreateCourse(courseId, groupId, 120));

        var userService = new Mock<IUserService>();
        userService
            .Setup(service => service.GetAllFamilyUsersInformation(familyId, true))
            .ReturnsAsync(new[]
            {
                new UserInformationResponse
                {
                    Email = "mother@example.com",
                    Relationship = Relationship.Mother
                }
            });

        var service = CreateEnrollmentService(
            repository: repository,
            transactionService: transactionService,
            courseService: courseService,
            userService: userService);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddEnrollment(new AddStudentCourseEnrollment
        {
            ChildId = childId,
            FamilyId = familyId,
            CourseId = courseId,
            CourseEnrollmentGroupId = groupId,
            WillUseDayCare = false,
            DayCareDays = 0
        }));

        Assert.Equal("Both mother and father must be registered before enrolling the child in this course.", exception.Message);
        repository.Verify(repo => repo.AddEnrollment(It.IsAny<AddStudentCourseEnrollment>()), Times.Never);
    }

    [Fact]
    public async Task AddEnrollment_WhenCourseIsEvent_AllowsEnrollmentWithoutBothParents()
    {
        var courseId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        AddStudentCourseEnrollment? capturedEnrollment = null;

        var repository = new Mock<IStudentCourseEnrollmentRepository>();
        repository
            .Setup(repo => repo.GetCourseEnrollmentGroupInformation(groupId))
            .ReturnsAsync(new CourseEnrollmentGroupInformationResponse
            {
                CourseEnrollmentGroupId = groupId,
                CourseId = courseId,
                MaxStudents = 20,
                IfRegistrationOpen = true,
                EnrollmentStatusCount = new Dictionary<EnrollmentStatus, int>()
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

        var course = CreateCourse(courseId, groupId, 120);
        course.IsCourseAnEvent = true;

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetCourse(courseId))
            .ReturnsAsync(course);

        var policyService = new Mock<IInstitutePolicyService>();
        policyService
            .Setup(service => service.GetAllPolicies(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<InstitutePolicyResponse>());

        var userService = new Mock<IUserService>();
        userService
            .Setup(service => service.GetAllFamilyUsersInformation(familyId, true))
            .ReturnsAsync(new[]
            {
                new UserInformationResponse
                {
                    Email = "mother@example.com",
                    Relationship = Relationship.Mother
                }
            });

        var service = CreateEnrollmentService(
            repository: repository,
            transactionService: transactionService,
            courseService: courseService,
            policyService: policyService,
            userService: userService);

        var response = await service.AddEnrollment(new AddStudentCourseEnrollment
        {
            ChildId = childId,
            FamilyId = familyId,
            CourseId = courseId,
            CourseEnrollmentGroupId = groupId,
            WillUseDayCare = false,
            DayCareDays = 0
        });

        Assert.NotNull(response);
        Assert.NotNull(capturedEnrollment);
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
                    PolicyType = PolicyType.CourseFeePayment,
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
        Assert.Equal(PaymentStatus.Unpaid, installment.PaymentStatus);
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
                    PolicyType = PolicyType.CourseFeePayment,
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
        Assert.All(capturedUpdate.FeeInstallments, installment => Assert.Equal(PaymentStatus.Unpaid, installment.PaymentStatus));
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
            .ReturnsAsync(CreateCourse(
                courseId,
                groupIds[0],
                100,
                ifRegistrationOpen: true,
                registrationFee: 50,
                courseIsRegistrationOpen: true,
                isManualEnrollment: false,
                otherGroupIds: new[] { groupIds[1], groupIds[2] }));

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
                    PolicyType = PolicyType.CourseFeePayment,
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
        Assert.All(capturedUpdate.FeeInstallments, installment => Assert.Equal(PaymentStatus.Unpaid, installment.PaymentStatus));
    }

    [Fact]
    public async Task RecalculateCourseFee_PreservesExistingSurchargeInTotalPayable()
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
                CreateFamilyTransaction(
                    courseId,
                    familyId,
                    new[]
                    {
                        CreateEnrollment(childId, groupId, courseId)
                    },
                    surcharge: 15d)
            });
        transactionService
            .Setup(service => service.UpdateTransaction(It.IsAny<Guid>(), It.IsAny<AddStudentCourseTransaction>()))
            .Callback<Guid, AddStudentCourseTransaction>((_, transaction) => capturedUpdate = transaction)
            .ReturnsAsync(true);

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetCourse(courseId))
            .ReturnsAsync(CreateCourse(courseId, groupId, 100));

        var policyService = new Mock<IInstitutePolicyService>();
        policyService
            .Setup(service => service.GetAllPolicies(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<InstitutePolicyResponse>());

        var service = CreateEnrollmentService(
            transactionService: transactionService,
            courseService: courseService,
            policyService: policyService);

        var result = await service.RecalculateCourseFee(courseId, familyId);

        Assert.True(result);
        Assert.NotNull(capturedUpdate);
        Assert.Equal(15d, capturedUpdate!.Surcharge);
        Assert.Equal(115m, capturedUpdate.TotalPayable);
    }

    [Fact]
    public async Task RecalculateCourseFee_WhenDifferentChildrenShareEnrollmentIndex_CalculatesFeeForBothChildren()
    {
        var courseId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var firstChildId = Guid.NewGuid();
        var secondChildId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var capturedUpdate = default(AddStudentCourseTransaction);

        var transactionService = new Mock<IStudentCourseTransactionService>();
        transactionService
            .Setup(service => service.GetCourseTransactionsByFamily(courseId, familyId))
            .ReturnsAsync(new[]
            {
                CreateFamilyTransaction(
                    courseId,
                    familyId,
                    new[]
                    {
                        CreateEnrollment(firstChildId, groupId, courseId, familyId, EnrollmentStatus.Awaiting, "First Child", enrollmentIndex: 2),
                        CreateEnrollment(secondChildId, groupId, courseId, familyId, EnrollmentStatus.Awaiting, "Second Child", enrollmentIndex: 2)
                    })
            });
        transactionService
            .Setup(service => service.UpdateTransaction(It.IsAny<Guid>(), It.IsAny<AddStudentCourseTransaction>()))
            .Callback<Guid, AddStudentCourseTransaction>((_, transaction) => capturedUpdate = transaction)
            .ReturnsAsync(true);

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetCourse(courseId))
            .ReturnsAsync(CreateCourse(courseId, groupId, 100));

        var policyService = new Mock<IInstitutePolicyService>();
        policyService
            .Setup(service => service.GetAllPolicies(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<InstitutePolicyResponse>());

        var service = CreateEnrollmentService(
            transactionService: transactionService,
            courseService: courseService,
            policyService: policyService);

        var result = await service.RecalculateCourseFee(courseId, familyId);

        Assert.True(result);
        Assert.NotNull(capturedUpdate);
        Assert.Equal(200m, capturedUpdate!.PayableFee);
        Assert.Equal(200m, capturedUpdate.TotalPayable);
    }

    [Fact]
    public async Task RecalculateCourseFee_AssignsInstallmentPaymentStatusesFromTotalPaidSoFar()
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
                CreateFamilyTransaction(
                    courseId,
                    familyId,
                    new[]
                    {
                        CreateEnrollment(childId, groupIds[0], courseId),
                        CreateEnrollment(childId, groupIds[1], courseId),
                        CreateEnrollment(childId, groupIds[2], courseId)
                    },
                    totalAmountPaid: 150m)
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
                    PolicyType = PolicyType.CourseFeePayment,
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
        Assert.Equal(PaymentStatus.Paid, capturedUpdate!.FeeInstallments[0].PaymentStatus);
        Assert.Equal(PaymentStatus.PartiallyPaid, capturedUpdate.FeeInstallments[1].PaymentStatus);
        Assert.Equal(PaymentStatus.Unpaid, capturedUpdate.FeeInstallments[2].PaymentStatus);
    }

    [Fact]
    public async Task RecalculateCourseFee_WhenCompletelyPaid_PromotesEnrolledStatusesToRegistered()
    {
        var courseId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var enrolledEnrollment = CreateEnrollment(childId, groupId, courseId, familyId, EnrollmentStatus.Enrolled, "Maryam");
        MultiUserEmailData? sentEmail = null;

        var repository = new Mock<IStudentCourseEnrollmentRepository>();
        repository
            .Setup(repo => repo.UpdateEnrollmentStatus(enrolledEnrollment.StudentCourseEnrollmentId, EnrollmentStatus.Registered))
            .ReturnsAsync(true);

        var transactionService = new Mock<IStudentCourseTransactionService>();
        transactionService
            .Setup(service => service.GetCourseTransactionsByFamily(courseId, familyId))
            .ReturnsAsync(new[]
            {
                CreateFamilyTransaction(
                    courseId,
                    familyId,
                    new[] { enrolledEnrollment },
                    totalAmountPaid: 100m)
            });
        transactionService
            .Setup(service => service.UpdateTransaction(It.IsAny<Guid>(), It.IsAny<AddStudentCourseTransaction>()))
            .ReturnsAsync(true);

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetCourse(courseId))
            .ReturnsAsync(CreateCourse(courseId, groupId, 100));

        var policyService = new Mock<IInstitutePolicyService>();
        policyService
            .Setup(service => service.GetAllPolicies(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<InstitutePolicyResponse>());

        var userService = new Mock<IUserService>();
        userService
            .Setup(service => service.GetAllFamilyUsersInformation(familyId, true))
            .ReturnsAsync(new[]
            {
                new UserInformationResponse
                {
                    Email = "parent@example.com",
                    Relationship = Relationship.Mother,
                    IfTempUser = false
                }
            });

        var sendEmailService = new Mock<ISendEmailService>();
        sendEmailService
            .Setup(service => service.SendBulkEmail(It.IsAny<MultiUserEmailData>()))
            .Callback<MultiUserEmailData>(email => sentEmail = email)
            .ReturnsAsync(true);

        var service = CreateEnrollmentService(
            repository: repository,
            transactionService: transactionService,
            courseService: courseService,
            policyService: policyService,
            userService: userService,
            sendEmailService: sendEmailService);

        var result = await service.RecalculateCourseFee(courseId, familyId);

        Assert.True(result);
        repository.Verify(
            repo => repo.UpdateEnrollmentStatus(enrolledEnrollment.StudentCourseEnrollmentId, EnrollmentStatus.Registered),
            Times.Once);
        Assert.NotNull(sentEmail);
        Assert.Contains("Registration Confirmation", sentEmail!.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Maryam", sentEmail.Body);
        sendEmailService.Verify(service => service.SendBulkEmail(It.IsAny<MultiUserEmailData>()), Times.Once);
    }

    [Fact]
    public async Task RecalculateCourseFee_WhenOnlyAwaitingEnrollmentsExist_DoesNotPromoteOrSendRegisteredEmail()
    {
        var courseId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var awaitingEnrollment = CreateEnrollment(childId, groupId, courseId, familyId, EnrollmentStatus.Awaiting, "Maryam");

        var repository = new Mock<IStudentCourseEnrollmentRepository>();

        var transactionService = new Mock<IStudentCourseTransactionService>();
        transactionService
            .Setup(service => service.GetCourseTransactionsByFamily(courseId, familyId))
            .ReturnsAsync(new[]
            {
                CreateFamilyTransaction(
                    courseId,
                    familyId,
                    new[] { awaitingEnrollment },
                    totalAmountPaid: 100m)
            });
        transactionService
            .Setup(service => service.UpdateTransaction(It.IsAny<Guid>(), It.IsAny<AddStudentCourseTransaction>()))
            .ReturnsAsync(true);

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetCourse(courseId))
            .ReturnsAsync(CreateCourse(courseId, groupId, 100));

        var policyService = new Mock<IInstitutePolicyService>();
        policyService
            .Setup(service => service.GetAllPolicies(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<InstitutePolicyResponse>());

        var sendEmailService = new Mock<ISendEmailService>();

        var service = CreateEnrollmentService(
            repository: repository,
            transactionService: transactionService,
            courseService: courseService,
            policyService: policyService,
            sendEmailService: sendEmailService);

        var result = await service.RecalculateCourseFee(courseId, familyId);

        Assert.True(result);
        repository.Verify(
            repo => repo.UpdateEnrollmentStatus(It.IsAny<Guid>(), EnrollmentStatus.Registered),
            Times.Never);
        sendEmailService.Verify(service => service.SendBulkEmail(It.IsAny<MultiUserEmailData>()), Times.Never);
    }

    [Fact]
    public async Task RecalculateCourseFee_WhenManualEnrollmentCourseIsFullyPaid_KeepsAwaitingStatusAndDoesNotSendRegisteredEmail()
    {
        var courseId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var awaitingEnrollment = CreateEnrollment(childId, groupId, courseId, familyId, EnrollmentStatus.Awaiting, "Maryam");
        AddStudentCourseTransaction? capturedUpdate = null;

        var repository = new Mock<IStudentCourseEnrollmentRepository>();

        var transactionService = new Mock<IStudentCourseTransactionService>();
        transactionService
            .Setup(service => service.GetCourseTransactionsByFamily(courseId, familyId))
            .ReturnsAsync(new[]
            {
                CreateFamilyTransaction(
                    courseId,
                    familyId,
                    new[] { awaitingEnrollment },
                    totalAmountPaid: 100m)
            });
        transactionService
            .Setup(service => service.UpdateTransaction(It.IsAny<Guid>(), It.IsAny<AddStudentCourseTransaction>()))
            .Callback<Guid, AddStudentCourseTransaction>((_, transaction) => capturedUpdate = transaction)
            .ReturnsAsync(true);

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetCourse(courseId))
            .ReturnsAsync(CreateCourse(courseId, groupId, 100, ifRegistrationOpen: true, registrationFee: 0, courseIsRegistrationOpen: true, isManualEnrollment: true));

        var policyService = new Mock<IInstitutePolicyService>();
        policyService
            .Setup(service => service.GetAllPolicies(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<InstitutePolicyResponse>());

        var sendEmailService = new Mock<ISendEmailService>();

        var service = CreateEnrollmentService(
            repository: repository,
            transactionService: transactionService,
            courseService: courseService,
            policyService: policyService,
            sendEmailService: sendEmailService);

        var result = await service.RecalculateCourseFee(courseId, familyId);

        Assert.True(result);
        Assert.NotNull(capturedUpdate);
        Assert.True(capturedUpdate!.IsCompletelyPaid);
        Assert.Equal(RegistrationStatus.Pending, capturedUpdate.RegistrationStatus);
        repository.Verify(
            repo => repo.UpdateEnrollmentStatus(It.IsAny<Guid>(), EnrollmentStatus.Registered),
            Times.Never);
        sendEmailService.Verify(service => service.SendBulkEmail(It.IsAny<MultiUserEmailData>()), Times.Never);
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
    public async Task UpdateEnrollment_WhenManualCourseAdminRegistersEnrollment_SendsNormalRegisteredEmail()
    {
        var enrollmentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        MultiUserEmailData? sentEmail = null;

        var repository = new Mock<IStudentCourseEnrollmentRepository>();
        repository
            .Setup(repo => repo.GetEnrollment(enrollmentId))
            .ReturnsAsync(CreateEnrollment(childId, groupId, courseId, familyId, EnrollmentStatus.Awaiting, "Maryam"));
        repository
            .Setup(repo => repo.GetCourseEnrollmentGroupInformation(groupId))
            .ReturnsAsync(new CourseEnrollmentGroupInformationResponse
            {
                CourseEnrollmentGroupId = groupId,
                CourseId = courseId,
                MaxStudents = 10,
                IfRegistrationOpen = true,
                EnrollmentStatusCount = new Dictionary<EnrollmentStatus, int>
                {
                    [EnrollmentStatus.Enrolled] = 1,
                    [EnrollmentStatus.Registered] = 1
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
                CreateFamilyTransaction(
                    courseId,
                    familyId,
                    new[]
                    {
                        CreateEnrollment(childId, groupId, courseId, familyId, EnrollmentStatus.Registered, "Maryam")
                    },
                    totalAmountPaid: 100m)
            });
        transactionService
            .Setup(service => service.UpdateTransaction(It.IsAny<Guid>(), It.IsAny<AddStudentCourseTransaction>()))
            .ReturnsAsync(true);

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetCourse(courseId))
            .ReturnsAsync(CreateCourse(courseId, groupId, 100, ifRegistrationOpen: true, registrationFee: 0, courseIsRegistrationOpen: true, isManualEnrollment: true));

        var policyService = new Mock<IInstitutePolicyService>();
        policyService
            .Setup(service => service.GetAllPolicies(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<InstitutePolicyResponse>());

        var userService = new Mock<IUserService>();
        userService
            .Setup(service => service.GetAllFamilyUsersInformation(familyId, true))
            .ReturnsAsync(new[]
            {
                new UserInformationResponse
                {
                    Email = "parent@example.com",
                    Relationship = Relationship.Father,
                    IfTempUser = false
                }
            });

        var sendEmailService = new Mock<ISendEmailService>();
        sendEmailService
            .Setup(service => service.SendBulkEmail(It.IsAny<MultiUserEmailData>()))
            .Callback<MultiUserEmailData>(email => sentEmail = email)
            .ReturnsAsync(true);

        var service = CreateEnrollmentService(
            repository: repository,
            transactionService: transactionService,
            courseService: courseService,
            policyService: policyService,
            userService: userService,
            sendEmailService: sendEmailService);

        var result = await service.UpdateEnrollment(enrollmentId, new AddStudentCourseEnrollment
        {
            ChildId = childId,
            FamilyId = familyId,
            CourseId = courseId,
            CourseEnrollmentGroupId = groupId,
            EnrollmentStatus = EnrollmentStatus.Registered,
            WillUseDayCare = false,
            DayCareDays = 0
        }, ifUpdatedByAdmin: true);

        Assert.True(result);
        Assert.NotNull(sentEmail);
        Assert.Contains("Registration Confirmation", sentEmail!.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Maryam", sentEmail.Body);
        Assert.Contains("Summer Camp", sentEmail.Body);
    }

    [Fact]
    public async Task UpdateEnrollment_WhenShouldTriggerEmailIsFalse_SkipsStatusEmail()
    {
        var enrollmentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var repository = new Mock<IStudentCourseEnrollmentRepository>();
        repository
            .Setup(repo => repo.GetEnrollment(enrollmentId))
            .ReturnsAsync(CreateEnrollment(childId, groupId, courseId, familyId, EnrollmentStatus.Awaiting, "Maryam"));
        repository
            .Setup(repo => repo.GetCourseEnrollmentGroupInformation(groupId))
            .ReturnsAsync(new CourseEnrollmentGroupInformationResponse
            {
                CourseEnrollmentGroupId = groupId,
                CourseId = courseId,
                MaxStudents = 10,
                IfRegistrationOpen = true,
                EnrollmentStatusCount = new Dictionary<EnrollmentStatus, int>
                {
                    [EnrollmentStatus.Enrolled] = 1,
                    [EnrollmentStatus.Registered] = 1
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
                CreateFamilyTransaction(
                    courseId,
                    familyId,
                    new[]
                    {
                        CreateEnrollment(childId, groupId, courseId, familyId, EnrollmentStatus.Registered, "Maryam")
                    },
                    totalAmountPaid: 100m)
            });
        transactionService
            .Setup(service => service.UpdateTransaction(It.IsAny<Guid>(), It.IsAny<AddStudentCourseTransaction>()))
            .ReturnsAsync(true);

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetCourse(courseId))
            .ReturnsAsync(CreateCourse(courseId, groupId, 100, ifRegistrationOpen: true, registrationFee: 0, courseIsRegistrationOpen: true, isManualEnrollment: true));

        var policyService = new Mock<IInstitutePolicyService>();
        policyService
            .Setup(service => service.GetAllPolicies(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<InstitutePolicyResponse>());

        var userService = new Mock<IUserService>();
        userService
            .Setup(service => service.GetAllFamilyUsersInformation(familyId, true))
            .ReturnsAsync(new[]
            {
                new UserInformationResponse
                {
                    Email = "parent@example.com",
                    Relationship = Relationship.Father
                }
            });

        var sendEmailService = new Mock<ISendEmailService>();

        var service = CreateEnrollmentService(
            repository: repository,
            transactionService: transactionService,
            courseService: courseService,
            policyService: policyService,
            userService: userService,
            sendEmailService: sendEmailService);

        var result = await service.UpdateEnrollment(enrollmentId, new AddStudentCourseEnrollment
        {
            ChildId = childId,
            FamilyId = familyId,
            CourseId = courseId,
            CourseEnrollmentGroupId = groupId,
            EnrollmentStatus = EnrollmentStatus.Registered,
            WillUseDayCare = false,
            DayCareDays = 0,
            ShouldTriggerEmail = false
        }, ifUpdatedByAdmin: true);

        Assert.True(result);
        sendEmailService.Verify(service => service.SendBulkEmail(It.IsAny<MultiUserEmailData>()), Times.Never);
    }

    [Fact]
    public async Task UpdateEnrollmentsBatch_SendsSingleFamilyEmailAndRecalculatesOncePerCourseFamily()
    {
        var enrollmentIdOne = Guid.NewGuid();
        var enrollmentIdTwo = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var childIdOne = Guid.NewGuid();
        var childIdTwo = Guid.NewGuid();
        var groupIdOne = Guid.NewGuid();
        var groupIdTwo = Guid.NewGuid();
        MultiUserEmailData? sentEmail = null;

        var repository = new Mock<IStudentCourseEnrollmentRepository>();
        repository
            .Setup(repo => repo.GetEnrollment(enrollmentIdOne))
            .ReturnsAsync(CreateEnrollment(childIdOne, groupIdOne, courseId, familyId, EnrollmentStatus.Enrolled, "Maryam"));
        repository
            .Setup(repo => repo.GetEnrollment(enrollmentIdTwo))
            .ReturnsAsync(CreateEnrollment(childIdTwo, groupIdTwo, courseId, familyId, EnrollmentStatus.Enrolled, "Yusuf"));
        repository
            .Setup(repo => repo.UpdateEnrollment(It.IsAny<Guid>(), It.IsAny<AddStudentCourseEnrollment>()))
            .ReturnsAsync(true);

        var transactionService = new Mock<IStudentCourseTransactionService>();
        transactionService
            .Setup(service => service.GetCourseTransactionsByFamily(courseId, familyId))
            .ReturnsAsync(new[]
            {
                CreateFamilyTransaction(courseId, familyId, new[]
                {
                    CreateEnrollment(childIdOne, groupIdOne, courseId, familyId, EnrollmentStatus.Enrolled, "Maryam"),
                    CreateEnrollment(childIdTwo, groupIdTwo, courseId, familyId, EnrollmentStatus.Enrolled, "Yusuf")
                })
            });
        transactionService
            .Setup(service => service.UpdateTransaction(It.IsAny<Guid>(), It.IsAny<AddStudentCourseTransaction>()))
            .ReturnsAsync(true);

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetCourse(courseId))
            .ReturnsAsync(CreateCourse(
                courseId,
                groupIdOne,
                100,
                ifRegistrationOpen: true,
                registrationFee: 0,
                courseIsRegistrationOpen: true,
                isManualEnrollment: false,
                otherGroupIds: new[] { groupIdTwo }));

        var policyService = new Mock<IInstitutePolicyService>();
        policyService
            .Setup(service => service.GetAllPolicies(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<InstitutePolicyResponse>());

        var userService = new Mock<IUserService>();
        userService
            .Setup(service => service.GetAllFamilyUsersInformation(familyId, true))
            .ReturnsAsync(new[]
            {
                new UserInformationResponse
                {
                    Email = "parent1@example.com",
                    Relationship = Relationship.Mother,
                    IfTempUser = false
                },
                new UserInformationResponse
                {
                    Email = "parent2@example.com",
                    Relationship = Relationship.Father,
                    IfTempUser = false
                }
            });

        var sendEmailService = new Mock<ISendEmailService>();
        sendEmailService
            .Setup(service => service.SendBulkEmail(It.IsAny<MultiUserEmailData>()))
            .Callback<MultiUserEmailData>(email => sentEmail = email)
            .ReturnsAsync(true);

        var service = CreateEnrollmentService(
            repository: repository,
            transactionService: transactionService,
            courseService: courseService,
            policyService: policyService,
            userService: userService,
            sendEmailService: sendEmailService);

        var result = await service.UpdateEnrollmentsBatch(new UpdateStudentCourseEnrollmentsBatchRequest
        {
            Enrollments = new List<UpdateStudentCourseEnrollmentBatchItem>
            {
                new()
                {
                    EnrollmentId = enrollmentIdOne,
                    Enrollment = new AddStudentCourseEnrollment
                    {
                        ChildId = childIdOne,
                        FamilyId = familyId,
                        CourseId = courseId,
                        CourseEnrollmentGroupId = groupIdOne,
                        EnrollmentStatus = EnrollmentStatus.Registered,
                        WillUseDayCare = false,
                        DayCareDays = 0
                    }
                },
                new()
                {
                    EnrollmentId = enrollmentIdTwo,
                    Enrollment = new AddStudentCourseEnrollment
                    {
                        ChildId = childIdTwo,
                        FamilyId = familyId,
                        CourseId = courseId,
                        CourseEnrollmentGroupId = groupIdTwo,
                        EnrollmentStatus = EnrollmentStatus.Registered,
                        WillUseDayCare = false,
                        DayCareDays = 0
                    }
                }
            }
        }, ifUpdatedByAdmin: true);

        Assert.True(result);
        Assert.NotNull(sentEmail);
        Assert.Contains("Maryam", sentEmail!.Body);
        Assert.Contains("Yusuf", sentEmail.Body);
        sendEmailService.Verify(service => service.SendBulkEmail(It.IsAny<MultiUserEmailData>()), Times.Once);
        transactionService.Verify(service => service.UpdateTransaction(It.IsAny<Guid>(), It.IsAny<AddStudentCourseTransaction>()), Times.Once);
        repository.Verify(repo => repo.UpdateEnrollment(It.IsAny<Guid>(), It.IsAny<AddStudentCourseEnrollment>()), Times.Exactly(2));
    }

    [Fact]
    public async Task UpdateEnrollmentsBatch_WhenMultipleFamilies_SendsSeparateEmailsPerFamily()
    {
        var enrollmentIdOne = Guid.NewGuid();
        var enrollmentIdTwo = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var familyIdOne = Guid.NewGuid();
        var familyIdTwo = Guid.NewGuid();
        var childIdOne = Guid.NewGuid();
        var childIdTwo = Guid.NewGuid();
        var groupIdOne = Guid.NewGuid();
        var groupIdTwo = Guid.NewGuid();
        var sentEmails = new List<MultiUserEmailData>();

        var repository = new Mock<IStudentCourseEnrollmentRepository>();
        repository
            .Setup(repo => repo.GetEnrollment(enrollmentIdOne))
            .ReturnsAsync(CreateEnrollment(childIdOne, groupIdOne, courseId, familyIdOne, EnrollmentStatus.Enrolled, "Maryam"));
        repository
            .Setup(repo => repo.GetEnrollment(enrollmentIdTwo))
            .ReturnsAsync(CreateEnrollment(childIdTwo, groupIdTwo, courseId, familyIdTwo, EnrollmentStatus.Enrolled, "Yusuf"));
        repository
            .Setup(repo => repo.UpdateEnrollment(It.IsAny<Guid>(), It.IsAny<AddStudentCourseEnrollment>()))
            .ReturnsAsync(true);

        var transactionService = new Mock<IStudentCourseTransactionService>();
        transactionService
            .Setup(service => service.GetCourseTransactionsByFamily(courseId, familyIdOne))
            .ReturnsAsync(new[]
            {
                CreateFamilyTransaction(courseId, familyIdOne, new[]
                {
                    CreateEnrollment(childIdOne, groupIdOne, courseId, familyIdOne, EnrollmentStatus.Enrolled, "Maryam")
                })
            });
        transactionService
            .Setup(service => service.GetCourseTransactionsByFamily(courseId, familyIdTwo))
            .ReturnsAsync(new[]
            {
                CreateFamilyTransaction(courseId, familyIdTwo, new[]
                {
                    CreateEnrollment(childIdTwo, groupIdTwo, courseId, familyIdTwo, EnrollmentStatus.Enrolled, "Yusuf")
                })
            });
        transactionService
            .Setup(service => service.UpdateTransaction(It.IsAny<Guid>(), It.IsAny<AddStudentCourseTransaction>()))
            .ReturnsAsync(true);

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetCourse(courseId))
            .ReturnsAsync(CreateCourse(
                courseId,
                groupIdOne,
                100,
                ifRegistrationOpen: true,
                registrationFee: 0,
                courseIsRegistrationOpen: true,
                isManualEnrollment: false,
                otherGroupIds: new[] { groupIdTwo }));

        var policyService = new Mock<IInstitutePolicyService>();
        policyService
            .Setup(service => service.GetAllPolicies(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<InstitutePolicyResponse>());

        var userService = new Mock<IUserService>();
        userService
            .Setup(service => service.GetAllFamilyUsersInformation(familyIdOne, true))
            .ReturnsAsync(new[]
            {
                new UserInformationResponse
                {
                    Email = "family1@example.com",
                    Relationship = Relationship.Mother,
                    IfTempUser = false
                }
            });
        userService
            .Setup(service => service.GetAllFamilyUsersInformation(familyIdTwo, true))
            .ReturnsAsync(new[]
            {
                new UserInformationResponse
                {
                    Email = "family2@example.com",
                    Relationship = Relationship.Father,
                    IfTempUser = false
                }
            });

        var sendEmailService = new Mock<ISendEmailService>();
        sendEmailService
            .Setup(service => service.SendBulkEmail(It.IsAny<MultiUserEmailData>()))
            .Callback<MultiUserEmailData>(email => sentEmails.Add(email))
            .ReturnsAsync(true);

        var service = CreateEnrollmentService(
            repository: repository,
            transactionService: transactionService,
            courseService: courseService,
            policyService: policyService,
            userService: userService,
            sendEmailService: sendEmailService);

        var result = await service.UpdateEnrollmentsBatch(new UpdateStudentCourseEnrollmentsBatchRequest
        {
            Enrollments = new List<UpdateStudentCourseEnrollmentBatchItem>
            {
                new()
                {
                    EnrollmentId = enrollmentIdOne,
                    Enrollment = new AddStudentCourseEnrollment
                    {
                        ChildId = childIdOne,
                        FamilyId = familyIdOne,
                        CourseId = courseId,
                        CourseEnrollmentGroupId = groupIdOne,
                        EnrollmentStatus = EnrollmentStatus.Registered,
                        WillUseDayCare = false,
                        DayCareDays = 0
                    }
                },
                new()
                {
                    EnrollmentId = enrollmentIdTwo,
                    Enrollment = new AddStudentCourseEnrollment
                    {
                        ChildId = childIdTwo,
                        FamilyId = familyIdTwo,
                        CourseId = courseId,
                        CourseEnrollmentGroupId = groupIdTwo,
                        EnrollmentStatus = EnrollmentStatus.Registered,
                        WillUseDayCare = false,
                        DayCareDays = 0
                    }
                }
            }
        }, ifUpdatedByAdmin: true);

        Assert.True(result);
        Assert.Equal(2, sentEmails.Count);
        Assert.Contains(sentEmails, email => email.To.Single() == "family1@example.com" && email.Body.Contains("Maryam"));
        Assert.Contains(sentEmails, email => email.To.Single() == "family2@example.com" && email.Body.Contains("Yusuf"));
        sendEmailService.Verify(service => service.SendBulkEmail(It.IsAny<MultiUserEmailData>()), Times.Exactly(2));
        transactionService.Verify(service => service.UpdateTransaction(It.IsAny<Guid>(), It.IsAny<AddStudentCourseTransaction>()), Times.Exactly(2));
    }

    [Fact]
    public async Task UpdateEnrollmentsBatch_WhenCancelled_SendsCancellationEmail()
    {
        var enrollmentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        MultiUserEmailData? sentEmail = null;

        var repository = new Mock<IStudentCourseEnrollmentRepository>();
        repository
            .Setup(repo => repo.GetEnrollment(enrollmentId))
            .ReturnsAsync(CreateEnrollment(childId, groupId, courseId, familyId, EnrollmentStatus.Enrolled, "Maryam"));
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
                    CreateEnrollment(childId, groupId, courseId, familyId, EnrollmentStatus.Cancelled, "Maryam")
                })
            });
        transactionService
            .Setup(service => service.UpdateTransaction(It.IsAny<Guid>(), It.IsAny<AddStudentCourseTransaction>()))
            .ReturnsAsync(true);

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetCourse(courseId))
            .ReturnsAsync(CreateCourse(courseId, groupId, 100, ifRegistrationOpen: false, courseIsRegistrationOpen: true));

        var policyService = new Mock<IInstitutePolicyService>();
        policyService
            .Setup(service => service.GetAllPolicies(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<InstitutePolicyResponse>());

        var userService = new Mock<IUserService>();
        userService
            .Setup(service => service.GetAllFamilyUsersInformation(familyId, true))
            .ReturnsAsync(new[]
            {
                new UserInformationResponse
                {
                    Email = "parent@example.com",
                    Relationship = Relationship.Mother,
                    IfTempUser = false
                }
            });

        var sendEmailService = new Mock<ISendEmailService>();
        sendEmailService
            .Setup(service => service.SendBulkEmail(It.IsAny<MultiUserEmailData>()))
            .Callback<MultiUserEmailData>(email => sentEmail = email)
            .ReturnsAsync(true);

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
            policyService: policyService,
            groupService: groupService,
            userService: userService,
            sendEmailService: sendEmailService);

        var result = await service.UpdateEnrollmentsBatch(new UpdateStudentCourseEnrollmentsBatchRequest
        {
            Enrollments = new List<UpdateStudentCourseEnrollmentBatchItem>
            {
                new()
                {
                    EnrollmentId = enrollmentId,
                    Enrollment = new AddStudentCourseEnrollment
                    {
                        ChildId = childId,
                        FamilyId = familyId,
                        CourseId = courseId,
                        CourseEnrollmentGroupId = groupId,
                        EnrollmentStatus = EnrollmentStatus.Cancelled,
                        WillUseDayCare = false,
                        DayCareDays = 0
                    }
                }
            }
        }, ifUpdatedByAdmin: true);

        Assert.True(result);
        Assert.NotNull(sentEmail);
        Assert.Contains("Cancellation", sentEmail!.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("registration has been cancelled", sentEmail.Body, StringComparison.OrdinalIgnoreCase);
        sendEmailService.Verify(service => service.SendBulkEmail(It.IsAny<MultiUserEmailData>()), Times.Once);
    }

    [Fact]
    public async Task UpdateEnrollmentsBatch_WhenShouldTriggerEmailIsFalse_SkipsBatchStatusEmails()
    {
        var familyId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var firstEnrollmentId = Guid.NewGuid();
        var secondEnrollmentId = Guid.NewGuid();
        var firstChildId = Guid.NewGuid();
        var secondChildId = Guid.NewGuid();
        var firstGroupId = Guid.NewGuid();
        var secondGroupId = Guid.NewGuid();

        var repository = new Mock<IStudentCourseEnrollmentRepository>();
        repository
            .Setup(repo => repo.GetEnrollment(firstEnrollmentId))
            .ReturnsAsync(CreateEnrollment(firstChildId, firstGroupId, courseId, familyId, EnrollmentStatus.Awaiting, "Maryam"));
        repository
            .Setup(repo => repo.GetEnrollment(secondEnrollmentId))
            .ReturnsAsync(CreateEnrollment(secondChildId, secondGroupId, courseId, familyId, EnrollmentStatus.Awaiting, "Yusuf"));
        repository
            .Setup(repo => repo.GetCourseEnrollmentGroupInformation(It.IsAny<Guid>()))
            .ReturnsAsync((Guid groupId) => new CourseEnrollmentGroupInformationResponse
            {
                CourseEnrollmentGroupId = groupId,
                CourseId = courseId,
                MaxStudents = 10,
                IfRegistrationOpen = true,
                EnrollmentStatusCount = new Dictionary<EnrollmentStatus, int>
                {
                    [EnrollmentStatus.Enrolled] = 1,
                    [EnrollmentStatus.Registered] = 0
                }
            });
        repository
            .Setup(repo => repo.UpdateEnrollment(It.IsAny<Guid>(), It.IsAny<AddStudentCourseEnrollment>()))
            .ReturnsAsync(true);

        var transactionService = new Mock<IStudentCourseTransactionService>();
        transactionService
            .Setup(service => service.GetCourseTransactionsByFamily(courseId, familyId))
            .ReturnsAsync(new[]
            {
                CreateFamilyTransaction(
                    courseId,
                    familyId,
                    new[]
                    {
                        CreateEnrollment(firstChildId, firstGroupId, courseId, familyId, EnrollmentStatus.Registered, "Maryam"),
                        CreateEnrollment(secondChildId, secondGroupId, courseId, familyId, EnrollmentStatus.Registered, "Yusuf")
                    },
                    totalAmountPaid: 200m)
            });
        transactionService
            .Setup(service => service.UpdateTransaction(It.IsAny<Guid>(), It.IsAny<AddStudentCourseTransaction>()))
            .ReturnsAsync(true);

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetCourse(courseId))
            .ReturnsAsync(CreateCourse(
                courseId,
                firstGroupId,
                100,
                ifRegistrationOpen: true,
                registrationFee: 0,
                courseIsRegistrationOpen: true,
                isManualEnrollment: false,
                otherGroupIds: new[] { secondGroupId }));

        var policyService = new Mock<IInstitutePolicyService>();
        policyService
            .Setup(service => service.GetAllPolicies(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<InstitutePolicyResponse>());

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

        var service = CreateEnrollmentService(
            repository: repository,
            transactionService: transactionService,
            courseService: courseService,
            policyService: policyService,
            userService: userService,
            sendEmailService: sendEmailService);

        var result = await service.UpdateEnrollmentsBatch(new UpdateStudentCourseEnrollmentsBatchRequest
        {
            Enrollments = new List<UpdateStudentCourseEnrollmentBatchItem>
            {
                new()
                {
                    EnrollmentId = firstEnrollmentId,
                    Enrollment = new AddStudentCourseEnrollment
                    {
                        ChildId = firstChildId,
                        FamilyId = familyId,
                        CourseId = courseId,
                        CourseEnrollmentGroupId = firstGroupId,
                        EnrollmentStatus = EnrollmentStatus.Registered,
                        WillUseDayCare = false,
                        DayCareDays = 0,
                        ShouldTriggerEmail = false
                    }
                },
                new()
                {
                    EnrollmentId = secondEnrollmentId,
                    Enrollment = new AddStudentCourseEnrollment
                    {
                        ChildId = secondChildId,
                        FamilyId = familyId,
                        CourseId = courseId,
                        CourseEnrollmentGroupId = secondGroupId,
                        EnrollmentStatus = EnrollmentStatus.Registered,
                        WillUseDayCare = false,
                        DayCareDays = 0,
                        ShouldTriggerEmail = false
                    }
                }
            }
        }, ifUpdatedByAdmin: true);

        Assert.True(result);
        sendEmailService.Verify(service => service.SendBulkEmail(It.IsAny<MultiUserEmailData>()), Times.Never);
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
        Mock<IUserService>? userService = null,
        Mock<ISendEmailService>? sendEmailService = null)
    {
        if (userService == null)
        {
            userService = new Mock<IUserService>();
            userService
                .Setup(service => service.GetAllFamilyUsersInformation(It.IsAny<Guid>(), true))
                .ReturnsAsync(new[]
                {
                    new UserInformationResponse
                    {
                        Email = "mother@example.com",
                        Relationship = Relationship.Mother,
                        IfTempUser = false
                    },
                    new UserInformationResponse
                    {
                        Email = "father@example.com",
                        Relationship = Relationship.Father,
                        IfTempUser = false
                    }
                });
        }

        return new StudentCourseEnrollmentService(
            (repository ?? new Mock<IStudentCourseEnrollmentRepository>()).Object,
            (transactionService ?? new Mock<IStudentCourseTransactionService>()).Object,
            (courseService ?? new Mock<ICourseService>()).Object,
            (policyService ?? new Mock<IInstitutePolicyService>()).Object,
            (groupService ?? new Mock<ICourseEnrollmentGroupService>()).Object,
            (sendEmailService ?? new Mock<ISendEmailService>()).Object,
            userService.Object);
    }

    private static StudentCourseTransactionResponse CreateFamilyTransaction(
        Guid courseId,
        Guid familyId,
        IEnumerable<StudentCourseEnrollmentResponse> enrollments,
        decimal totalAmountPaid = 0m,
        double surcharge = 0d)
    {
        return new StudentCourseTransactionResponse
        {
            StudentCourseTransactionId = Guid.NewGuid(),
            FamilyId = familyId,
            PaymentCode = "PAY001",
            TransactionStatus = TransactionStatus.AwaitingPayment,
            RegistrationStatus = RegistrationStatus.Pending,
            IsActive = true,
            TotalAmountPaid = totalAmountPaid,
            FeeAmountDiscount = 0m,
            DayCareDiscount = 0m,
            Surcharge = surcharge,
            Enrollments = enrollments.ToList()
        };
    }

    private static StudentCourseEnrollmentResponse CreateEnrollment(
        Guid childId,
        Guid groupId,
        Guid courseId,
        Guid? familyId = null,
        EnrollmentStatus status = EnrollmentStatus.Registered,
        string childName = "Child",
        int enrollmentIndex = 1)
    {
        return new StudentCourseEnrollmentResponse
        {
            StudentCourseEnrollmentId = Guid.NewGuid(),
            ChildId = childId,
            CourseEnrollmentGroupId = groupId,
            CourseId = courseId,
            FamilyId = familyId ?? Guid.NewGuid(),
            EnrollmentStatus = status,
            ChildName = childName,
            WillUseDayCare = false,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            UpdatedOn = DateTime.UtcNow,
            EnrollmentIndex = enrollmentIndex
        };
    }

    private static CourseResponseDetailed CreateCourse(Guid courseId, Guid firstGroupId, int fee, params Guid[] otherGroupIds)
        => CreateCourse(
            courseId,
            firstGroupId,
            fee,
            ifRegistrationOpen: true,
            registrationFee: 0,
            courseIsRegistrationOpen: true,
            isManualEnrollment: false,
            otherGroupIds: otherGroupIds);

    private static CourseResponseDetailed CreateCourse(
        Guid courseId,
        Guid firstGroupId,
        int fee,
        bool ifRegistrationOpen,
        bool courseIsRegistrationOpen = true,
        params Guid[] otherGroupIds)
        => CreateCourse(
            courseId,
            firstGroupId,
            fee,
            ifRegistrationOpen: ifRegistrationOpen,
            registrationFee: 0,
            courseIsRegistrationOpen: courseIsRegistrationOpen,
            isManualEnrollment: false,
            otherGroupIds: otherGroupIds);

    private static CourseResponseDetailed CreateCourse(
        Guid courseId,
        Guid firstGroupId,
        int fee,
        bool ifRegistrationOpen,
        int registrationFee,
        bool courseIsRegistrationOpen = true,
        bool isManualEnrollment = false,
        params Guid[] otherGroupIds)
    {
        var groupIds = new[] { firstGroupId }.Concat(otherGroupIds).ToList();

        return new CourseResponseDetailed
        {
            CourseId = courseId,
            InstituteId = Guid.NewGuid(),
            RegistrationFee = registrationFee,
            IsRegistrationOpened = courseIsRegistrationOpen,
            IsManualEnrollment = isManualEnrollment,
            Name = "Summer Camp",
            NameFr = "Camp d'ete",
            CanSelectMultipleEnrollmentGroups = groupIds.Count > 1,
            CourseEnrollmentGroups = groupIds.Select((groupId, index) => new CourseEnrollmentGroupResponse
            {
                CourseEnrollmentGroupId = groupId,
                CourseId = courseId,
                InstituteId = Guid.NewGuid(),
                Fee = fee,
                DayCareFee = 0,
                IfRegistrationOpen = ifRegistrationOpen,
                GroupTitle = "Group",
                GroupTitleFr = "Groupe",
                Details = $"Group {index + 1}",
                DetailsFr = $"Groupe {index + 1}",
                AcedemicGroups = new List<string>()
            }).ToList()
        };
    }
}
