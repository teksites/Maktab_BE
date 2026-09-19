using Application.Users.Contracts;
using Courses.Implementation.Services;
using Courses.Repository;
using Courses.Services;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Models;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Attendance;
using MaktabDataContracts.Responses.Children;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Responses.Users;
using Moq;
using Users.Services;

namespace Courses.Test;

public class StudentCourseResultServiceTests
{
    [Theory]
    [InlineData(4, 3, 75, true)]
    [InlineData(0, 0, 100, false)]
    public async Task UpsertCourseChildResult_CalculatesAttendancePercentageAndUsesCourseInstituteId(
        int totalAttendanceRecords,
        int nonAbsentAttendanceRecords,
        decimal expectedAttendancePercentage,
        bool expectedHasAttendanceRecords)
    {
        var userId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var instituteId = Guid.NewGuid();

        Guid capturedChildId = Guid.Empty;
        Guid capturedCourseId = Guid.Empty;
        Guid capturedInstituteId = Guid.Empty;
        decimal? capturedAttendancePercentage = null;
        string? capturedRemarks = "not-captured";

        var repository = new Mock<IStudentCourseResultRepository>();
        repository
            .Setup(repo => repo.Upsert(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<decimal?>(),
                It.IsAny<StudentCourseResultStatus>(),
                It.IsAny<string?>(),
                It.IsAny<Guid>(),
                It.IsAny<bool>()))
            .Callback<Guid, Guid, Guid, Guid, decimal?, StudentCourseResultStatus, string?, Guid, bool>(
                (childArg, _, courseArg, instituteArg, attendanceArg, _, remarksArg, _, _) =>
                {
                    capturedChildId = childArg;
                    capturedCourseId = courseArg;
                    capturedInstituteId = instituteArg;
                    capturedAttendancePercentage = attendanceArg;
                    capturedRemarks = remarksArg;
                })
            .ReturnsAsync(new StudentCourseResultResponse
            {
                StudentCourseResultId = Guid.NewGuid(),
                ChildId = childId,
                FamilyId = familyId,
                CourseId = courseId,
                InstituteId = instituteId,
                AttendancePercentage = expectedAttendancePercentage,
                HasAttendanceRecords = expectedHasAttendanceRecords,
                HasResult = true,
                ResultStatus = StudentCourseResultStatus.Pass,
                Remarks = "Excellent",
                RecordedByUserId = userId,
                IsActive = true
            });

        var enrollmentService = new Mock<IStudentCourseEnrollmentService>();
        enrollmentService
            .Setup(service => service.GetAllEnrollments(courseId))
            .ReturnsAsync(new List<StudentCourseEnrollmentResponse>
            {
                new()
                {
                    StudentCourseEnrollmentId = Guid.NewGuid(),
                    ChildId = childId,
                    FamilyId = familyId,
                    CourseId = courseId,
                    CourseEnrollmentGroupId = Guid.NewGuid(),
                    ChildName = "Student One",
                    RegistrationNumber = "REG-1",
                    EnrollmentStatus = EnrollmentStatus.Enrolled,
                    IsActive = true
                },
                new()
                {
                    StudentCourseEnrollmentId = Guid.NewGuid(),
                    ChildId = childId,
                    FamilyId = familyId,
                    CourseId = courseId,
                    CourseEnrollmentGroupId = Guid.NewGuid(),
                    ChildName = "Student One",
                    RegistrationNumber = "REG-1",
                    EnrollmentStatus = EnrollmentStatus.Registered,
                    IsActive = true
                }
            });

        var attendanceRepository = new Mock<IStudentCourseAttendanceRepository>();
        attendanceRepository
            .Setup(repo => repo.GetAttendanceSummary(courseId, childId))
            .ReturnsAsync((totalAttendanceRecords, nonAbsentAttendanceRecords));

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetCourse(courseId))
            .ReturnsAsync(new CourseResponseDetailed
            {
                CourseId = courseId,
                InstituteId = instituteId,
                Name = "Quran"
            });

        var dataAccessVerificationService = new Mock<IDataAccessVerificationService>();
        dataAccessVerificationService
            .Setup(service => service.HasElevatedAccess(UserRoleType.SchoolAdmin))
            .Returns(true);

        var service = new StudentCourseResultService(
            repository.Object,
            enrollmentService.Object,
            attendanceRepository.Object,
            Mock.Of<ICourseStaffAssignmentService>(),
            courseService.Object,
            dataAccessVerificationService.Object,
            Mock.Of<IUserService>(),
            CreateChildService(childId, familyId).Object);

        var result = await service.UpsertCourseChildResult(userId, UserRoleType.SchoolAdmin, courseId, childId, new UpsertStudentCourseResultRequest
        {
            ResultStatus = StudentCourseResultStatus.Pass,
            IsActive = true
        });

        Assert.Equal(childId, capturedChildId);
        Assert.Equal(courseId, capturedCourseId);
        Assert.Equal(instituteId, capturedInstituteId);
        Assert.Equal(expectedAttendancePercentage, capturedAttendancePercentage);
        Assert.Null(capturedRemarks);
        Assert.Equal(expectedAttendancePercentage, result.AttendancePercentage);
        Assert.Equal(expectedHasAttendanceRecords, result.HasAttendanceRecords);
    }

    [Fact]
    public async Task GetCourseResults_MergesExistingResultsWithCourseCandidatesWithoutResults()
    {
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var instituteId = Guid.NewGuid();
        var childWithResultId = Guid.NewGuid();
        var childWithoutResultId = Guid.NewGuid();
        var familyOneId = Guid.NewGuid();
        var familyTwoId = Guid.NewGuid();

        var repository = new Mock<IStudentCourseResultRepository>();
        repository
            .Setup(repo => repo.GetByCourseId(courseId))
            .ReturnsAsync(new List<StudentCourseResultResponse>
            {
                new()
                {
                    StudentCourseResultId = Guid.NewGuid(),
                    ChildId = childWithResultId,
                    FamilyId = familyOneId,
                    CourseId = courseId,
                    InstituteId = instituteId,
                    CourseName = "Quran",
                    ChildName = "Student One",
                    ArabicName = "طالب واحد",
                    RegistrationNumber = "REG-1",
                    AttendancePercentage = 100m,
                    HasAttendanceRecords = true,
                    HasResult = true,
                    ResultStatus = StudentCourseResultStatus.Pass,
                    Remarks = "Done",
                    IsActive = true
                }
            });

        var enrollmentService = new Mock<IStudentCourseEnrollmentService>();
        enrollmentService
            .Setup(service => service.GetAllEnrollments(courseId))
            .ReturnsAsync(new List<StudentCourseEnrollmentResponse>
            {
                new()
                {
                    StudentCourseEnrollmentId = Guid.NewGuid(),
                    ChildId = childWithResultId,
                    FamilyId = familyOneId,
                    CourseId = courseId,
                    ChildName = "Student One",
                    ArabicName = "طالب واحد",
                    RegistrationNumber = "REG-1",
                    EnrollmentStatus = EnrollmentStatus.Enrolled,
                    EnrollmentIndex = 1,
                    CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new()
                {
                    StudentCourseEnrollmentId = Guid.NewGuid(),
                    ChildId = childWithoutResultId,
                    FamilyId = familyTwoId,
                    CourseId = courseId,
                    ChildName = "Student Two",
                    ArabicName = "طالب اثنان",
                    RegistrationNumber = "REG-2",
                    EnrollmentStatus = EnrollmentStatus.Registered,
                    EnrollmentIndex = 1,
                    CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            });

        var attendanceRepository = new Mock<IStudentCourseAttendanceRepository>();
        attendanceRepository
            .Setup(repo => repo.GetAttendanceSummariesByCourse(courseId))
            .ReturnsAsync(new Dictionary<Guid, (int TotalRecords, int PresentCount)>
            {
                [childWithResultId] = (4, 4),
                [childWithoutResultId] = (4, 3)
            });

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetCourse(courseId))
            .ReturnsAsync(new CourseResponseDetailed
            {
                CourseId = courseId,
                InstituteId = instituteId,
                Name = "Quran"
            });

        var dataAccessVerificationService = new Mock<IDataAccessVerificationService>();
        dataAccessVerificationService
            .Setup(service => service.HasElevatedAccess(UserRoleType.SchoolAdmin))
            .Returns(true);

        var service = new StudentCourseResultService(
            repository.Object,
            enrollmentService.Object,
            attendanceRepository.Object,
            Mock.Of<ICourseStaffAssignmentService>(),
            courseService.Object,
            dataAccessVerificationService.Object,
            Mock.Of<IUserService>(),
            Mock.Of<IUserChildrenService>());

        var results = (await service.GetCourseResults(userId, UserRoleType.SchoolAdmin, courseId)).ToList();

        Assert.Equal(2, results.Count);
        Assert.Contains(results, result => result.ChildId == childWithResultId && result.HasResult && result.AttendancePercentage == 100m && result.ArabicName == "طالب واحد");
        Assert.Contains(results, result => result.ChildId == childWithoutResultId && !result.HasResult && result.AttendancePercentage == 75m && result.ArabicName == "طالب اثنان");
    }

    [Fact]
    public async Task GetChildResults_WhenStaffRequestsHistoryWithoutCourseId_Throws()
    {
        var userId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var familyId = Guid.NewGuid();

        var dataAccessVerificationService = new Mock<IDataAccessVerificationService>();
        dataAccessVerificationService
            .Setup(service => service.HasElevatedAccess(UserRoleType.SchoolTeacher))
            .Returns(false);

        var service = new StudentCourseResultService(
            Mock.Of<IStudentCourseResultRepository>(),
            Mock.Of<IStudentCourseEnrollmentService>(),
            Mock.Of<IStudentCourseAttendanceRepository>(),
            Mock.Of<ICourseStaffAssignmentService>(),
            Mock.Of<ICourseService>(),
            dataAccessVerificationService.Object,
            Mock.Of<IUserService>(),
            CreateChildService(childId, familyId).Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetChildResults(userId, UserRoleType.SchoolTeacher, childId));
    }

    [Fact]
    public async Task GetFamilyResults_WhenNormalUserRequestsDifferentFamily_Throws()
    {
        var userId = Guid.NewGuid();
        var userFamilyId = Guid.NewGuid();
        var requestedFamilyId = Guid.NewGuid();

        var dataAccessVerificationService = new Mock<IDataAccessVerificationService>();
        dataAccessVerificationService
            .Setup(service => service.HasElevatedAccess(UserRoleType.Normal))
            .Returns(false);

        var userService = new Mock<IUserService>();
        userService
            .Setup(service => service.GetUserInformation(userId))
            .ReturnsAsync(new UserInformationResponse
            {
                UserId = userId,
                FamilyId = userFamilyId
            });

        var service = new StudentCourseResultService(
            Mock.Of<IStudentCourseResultRepository>(),
            Mock.Of<IStudentCourseEnrollmentService>(),
            Mock.Of<IStudentCourseAttendanceRepository>(),
            Mock.Of<ICourseStaffAssignmentService>(),
            Mock.Of<ICourseService>(),
            dataAccessVerificationService.Object,
            userService.Object,
            Mock.Of<IUserChildrenService>());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetFamilyResults(userId, UserRoleType.Normal, requestedFamilyId));
    }

    [Fact]
    public async Task GetCourseChildResult_WhenTeacherAssignedToCourseAndChildInAssignedGroup_ReturnsMergedResult()
    {
        var userId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var instituteId = Guid.NewGuid();

        var repository = new Mock<IStudentCourseResultRepository>();
        repository
            .Setup(repo => repo.GetByCourseId(courseId))
            .ReturnsAsync(new List<StudentCourseResultResponse>());

        var enrollmentService = new Mock<IStudentCourseEnrollmentService>();
        enrollmentService
            .Setup(service => service.GetAllEnrollments(courseId))
            .ReturnsAsync(new List<StudentCourseEnrollmentResponse>
            {
                new()
                {
                    StudentCourseEnrollmentId = Guid.NewGuid(),
                    ChildId = childId,
                    FamilyId = familyId,
                    CourseId = courseId,
                    CourseEnrollmentGroupId = groupId,
                    ChildName = "Student One",
                    RegistrationNumber = "REG-1",
                    EnrollmentStatus = EnrollmentStatus.Enrolled,
                    EnrollmentIndex = 1,
                    CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            });
        enrollmentService
            .Setup(service => service.GetEnrollmentsByGroup(groupId))
            .ReturnsAsync(new List<StudentCourseEnrollmentResponse>
            {
                new()
                {
                    StudentCourseEnrollmentId = Guid.NewGuid(),
                    ChildId = childId,
                    FamilyId = familyId,
                    CourseId = courseId,
                    CourseEnrollmentGroupId = groupId,
                    EnrollmentStatus = EnrollmentStatus.Enrolled
                }
            });

        var attendanceRepository = new Mock<IStudentCourseAttendanceRepository>();
        attendanceRepository
            .Setup(repo => repo.GetAttendanceSummariesByCourse(courseId))
            .ReturnsAsync(new Dictionary<Guid, (int TotalRecords, int PresentCount)>
            {
                [childId] = (2, 1)
            });

        var courseStaffAssignmentService = new Mock<ICourseStaffAssignmentService>();
        courseStaffAssignmentService
            .Setup(service => service.GetAssignedCourseGroups(userId, true))
            .ReturnsAsync(new List<TeacherAssignedCourseGroupResponse>
            {
                new()
                {
                    CourseId = courseId,
                    CourseEnrollmentGroupId = groupId
                }
            });

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetCourse(courseId))
            .ReturnsAsync(new CourseResponseDetailed
            {
                CourseId = courseId,
                InstituteId = instituteId,
                Name = "Quran"
            });

        var dataAccessVerificationService = new Mock<IDataAccessVerificationService>();
        dataAccessVerificationService
            .Setup(service => service.HasElevatedAccess(UserRoleType.SchoolTeacher))
            .Returns(false);

        var service = new StudentCourseResultService(
            repository.Object,
            enrollmentService.Object,
            attendanceRepository.Object,
            courseStaffAssignmentService.Object,
            courseService.Object,
            dataAccessVerificationService.Object,
            Mock.Of<IUserService>(),
            CreateChildService(childId, familyId).Object);

        var result = await service.GetCourseChildResult(userId, UserRoleType.SchoolTeacher, courseId, childId);

        Assert.NotNull(result);
        Assert.Equal(childId, result!.ChildId);
        Assert.Equal(50m, result.AttendancePercentage);
        Assert.False(result.HasResult);
    }

    private static Mock<IUserChildrenService> CreateChildService(Guid childId, Guid familyId)
    {
        var childService = new Mock<IUserChildrenService>();
        childService
            .Setup(service => service.GetChild(childId, null))
            .ReturnsAsync(new MaktabApiResult<ChildResponse>
            {
                Result = new ChildResponse
                {
                    ChildId = childId,
                    FamilyId = familyId
                }
            });

        return childService;
    }
}
