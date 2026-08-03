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
    [Fact]
    public async Task UpsertEnrollmentResult_CalculatesAttendancePercentageAndUsesGroupInstituteId()
    {
        var userId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var instituteId = Guid.NewGuid();

        Guid capturedInstituteId = Guid.Empty;
        decimal capturedAttendancePercentage = 0m;
        StudentCourseResultStatus capturedStatus = StudentCourseResultStatus.Unknown;
        string capturedRemarks = string.Empty;
        Guid capturedRecordedByUserId = Guid.Empty;

        var repository = new Mock<IStudentCourseResultRepository>();
        repository
            .Setup(repo => repo.Upsert(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<decimal>(),
                It.IsAny<StudentCourseResultStatus>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<bool>()))
            .Callback<Guid, Guid, Guid, Guid, Guid, Guid, decimal, StudentCourseResultStatus, string, Guid, bool>(
                (_, _, _, _, _, instituteArg, attendanceArg, statusArg, remarksArg, recordedByArg, _) =>
                {
                    capturedInstituteId = instituteArg;
                    capturedAttendancePercentage = attendanceArg;
                    capturedStatus = statusArg;
                    capturedRemarks = remarksArg;
                    capturedRecordedByUserId = recordedByArg;
                })
            .ReturnsAsync(new StudentCourseResultResponse
            {
                StudentCourseResultId = Guid.NewGuid(),
                StudentCourseEnrollmentId = enrollmentId,
                ChildId = childId,
                FamilyId = familyId,
                CourseId = courseId,
                CourseEnrollmentGroupId = groupId,
                InstituteId = instituteId,
                AttendancePercentage = 75m,
                ResultStatus = StudentCourseResultStatus.Pass,
                Remarks = "Excellent",
                RecordedByUserId = userId,
                IsActive = true
            });

        var enrollmentService = new Mock<IStudentCourseEnrollmentService>();
        enrollmentService
            .Setup(service => service.GetEnrollment(enrollmentId))
            .ReturnsAsync(new StudentCourseEnrollmentResponse
            {
                StudentCourseEnrollmentId = enrollmentId,
                ChildId = childId,
                FamilyId = familyId,
                CourseId = courseId,
                CourseEnrollmentGroupId = groupId
            });

        var attendanceRepository = new Mock<IStudentCourseAttendanceRepository>();
        attendanceRepository
            .Setup(repo => repo.GetAttendanceSummary(enrollmentId))
            .ReturnsAsync((4, 3));

        var groupService = new Mock<ICourseEnrollmentGroupService>();
        groupService
            .Setup(service => service.GetGroup(groupId))
            .ReturnsAsync(new CourseEnrollmentGroupResponse
            {
                CourseEnrollmentGroupId = groupId,
                CourseId = courseId,
                InstituteId = instituteId
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
            groupService.Object,
            dataAccessVerificationService.Object,
            Mock.Of<IUserService>(),
            Mock.Of<IUserChildrenService>());

        var result = await service.UpsertEnrollmentResult(userId, UserRoleType.SchoolAdmin, enrollmentId, new UpsertStudentCourseResultRequest
        {
            ResultStatus = StudentCourseResultStatus.Pass,
            Remarks = "Excellent",
            IsActive = true
        });

        Assert.Equal(instituteId, capturedInstituteId);
        Assert.Equal(75m, capturedAttendancePercentage);
        Assert.Equal(StudentCourseResultStatus.Pass, capturedStatus);
        Assert.Equal("Excellent", capturedRemarks);
        Assert.Equal(userId, capturedRecordedByUserId);
        Assert.Equal(instituteId, result.InstituteId);
        Assert.Equal(75m, result.AttendancePercentage);
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
            Mock.Of<ICourseEnrollmentGroupService>(),
            dataAccessVerificationService.Object,
            userService.Object,
            Mock.Of<IUserChildrenService>());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetFamilyResults(userId, UserRoleType.Normal, requestedFamilyId));
    }

    [Fact]
    public async Task GetChildResults_WhenTeacherIsAssignedToChildGroup_ReturnsResults()
    {
        var userId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var expected = new List<StudentCourseResultResponse>
        {
            new()
            {
                StudentCourseResultId = Guid.NewGuid(),
                ChildId = childId,
                FamilyId = familyId,
                CourseEnrollmentGroupId = groupId
            }
        };

        var childService = new Mock<IUserChildrenService>();
        childService
            .Setup(service => service.GetChild(childId))
            .ReturnsAsync(new MaktabApiResult<ChildResponse>
            {
                Result = new ChildResponse
                {
                    ChildId = childId,
                    FamilyId = familyId
                }
            });

        var courseStaffAssignmentService = new Mock<ICourseStaffAssignmentService>();
        courseStaffAssignmentService
            .Setup(service => service.GetAssignedCourseGroups(userId, true))
            .ReturnsAsync(new List<TeacherAssignedCourseGroupResponse>
            {
                new()
                {
                    CourseEnrollmentGroupId = groupId
                }
            });

        var enrollmentService = new Mock<IStudentCourseEnrollmentService>();
        enrollmentService
            .Setup(service => service.GetEnrollmentsByGroup(groupId))
            .ReturnsAsync(new List<StudentCourseEnrollmentResponse>
            {
                new()
                {
                    StudentCourseEnrollmentId = Guid.NewGuid(),
                    ChildId = childId,
                    FamilyId = familyId,
                    CourseEnrollmentGroupId = groupId
                }
            });

        var repository = new Mock<IStudentCourseResultRepository>();
        repository
            .Setup(repo => repo.GetByChildId(childId, null, null))
            .ReturnsAsync(expected);

        var dataAccessVerificationService = new Mock<IDataAccessVerificationService>();
        dataAccessVerificationService
            .Setup(service => service.HasElevatedAccess(UserRoleType.SchoolTeacher))
            .Returns(false);

        var service = new StudentCourseResultService(
            repository.Object,
            enrollmentService.Object,
            Mock.Of<IStudentCourseAttendanceRepository>(),
            courseStaffAssignmentService.Object,
            Mock.Of<ICourseEnrollmentGroupService>(),
            dataAccessVerificationService.Object,
            Mock.Of<IUserService>(),
            childService.Object);

        var result = await service.GetChildResults(userId, UserRoleType.SchoolTeacher, childId);

        Assert.Single(result);
        Assert.Equal(childId, result[0].ChildId);
        Assert.Equal(familyId, result[0].FamilyId);
    }
}
