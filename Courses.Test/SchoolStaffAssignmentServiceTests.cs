using Courses.Implementation.Services;
using Courses.Repository;
using Courses.Services;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Requests.InstituteStaff;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Responses.Institute;
using MaktabDataContracts.Responses.InstituteStaff;
using Moq;
using Users.Services;

namespace Courses.Test;

public class SchoolStaffAssignmentServiceTests
{
    [Fact]
    public async Task AddInstituteStaffAssignment_WhenActiveDatesOverlap_Throws()
    {
        var instituteId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var repository = new Mock<IInstituteStaffAssignmentRepository>();
        repository
            .Setup(repo => repo.GetUserSummary(userId))
            .ReturnsAsync(new InstituteStaffUserSummaryResponse
            {
                UserId = userId,
                GlobalUserRoles = UserRoleType.SchoolTeacher
            });
        repository
            .Setup(repo => repo.GetAssignments(instituteId, userId, false))
            .ReturnsAsync(new List<InstituteStaffAssignmentResponse>
            {
                new()
                {
                    InstituteStaffAssignmentId = Guid.NewGuid(),
                    InstituteId = instituteId,
                    StaffRoles = UserRoleType.SchoolTeacher,
                    StartDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                    EndDate = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true,
                    StaffUser = new InstituteStaffUserSummaryResponse { UserId = userId }
                }
            });

        var instituteService = new Mock<IInstituteService>();
        instituteService
            .Setup(service => service.GetInstitute(instituteId))
            .ReturnsAsync(new InstituteResponse { InstituteId = instituteId });

        var service = new InstituteStaffAssignmentService(repository.Object, instituteService.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddAssignment(new AddInstituteStaffAssignmentRequest
        {
            InstituteId = instituteId,
            UserId = userId,
            StaffRoles = UserRoleType.SchoolTeacher,
            StartDate = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true
        }));
    }

    [Fact]
    public async Task GetCourseGroupAssignments_WhenCourseAssignmentsExist_ReturnsInheritedAssignments()
    {
        var courseId = Guid.NewGuid();
        var instituteId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var repository = new Mock<ICourseStaffAssignmentRepository>();
        repository
            .Setup(repo => repo.GetCourseAssignments(courseId, true))
            .ReturnsAsync(new List<CourseStaffAssignmentResponse>
            {
                new()
                {
                    CourseStaffAssignmentId = Guid.NewGuid(),
                    CourseId = courseId,
                    InstituteId = instituteId,
                    AssignmentRoles = UserRoleType.SchoolTeacher,
                    StartDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true,
                    StaffUser = new InstituteStaffUserSummaryResponse
                    {
                        UserId = Guid.NewGuid(),
                        FirstName = "Teacher"
                    }
                }
            });

        var groupService = new Mock<ICourseEnrollmentGroupService>();
        groupService
            .Setup(service => service.GetCourseGroup(groupId))
            .ReturnsAsync(new CourseEnrollmentGroupResponse
            {
                CourseEnrollmentGroupId = groupId,
                CourseId = courseId,
                InstituteId = instituteId
            });

        var service = new CourseStaffAssignmentService(
            repository.Object,
            Mock.Of<ICourseService>(),
            groupService.Object,
            Mock.Of<IInstituteStaffAssignmentService>(),
            Mock.Of<IStudentCourseEnrollmentService>(),
            Mock.Of<IOtherContactsService>());

        var result = await service.GetCourseGroupAssignments(groupId);

        Assert.True(result.UsesCourseLevelAssignments);
        Assert.Single(result.StaffAssignments);
        Assert.True(result.StaffAssignments[0].IsInheritedFromCourse);
        Assert.Equal(groupId, result.StaffAssignments[0].CourseEnrollmentGroupId);
    }

    [Fact]
    public async Task SetCourseAssignments_WhenGroupAssignmentsAlreadyActive_Throws()
    {
        var courseId = Guid.NewGuid();
        var instituteId = Guid.NewGuid();

        var repository = new Mock<ICourseStaffAssignmentRepository>();
        repository
            .Setup(repo => repo.HasActiveDirectGroupAssignments(courseId))
            .ReturnsAsync(true);

        var courseService = new Mock<ICourseService>();
        courseService
            .Setup(service => service.GetCourse(courseId))
            .ReturnsAsync(new CourseResponseDetailed
            {
                CourseId = courseId,
                InstituteId = instituteId
            });

        var service = new CourseStaffAssignmentService(
            repository.Object,
            courseService.Object,
            Mock.Of<ICourseEnrollmentGroupService>(),
            Mock.Of<IInstituteStaffAssignmentService>(),
            Mock.Of<IStudentCourseEnrollmentService>(),
            Mock.Of<IOtherContactsService>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetCourseAssignments(new SetCourseStaffAssignmentsRequest
        {
            CourseId = courseId,
            InstituteId = instituteId,
            StaffAssignments =
            [
                new CourseStaffAssignmentItemRequest
                {
                    UserId = Guid.NewGuid(),
                    AssignmentRoles = UserRoleType.SchoolTeacher,
                    StartDate = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true
                }
            ]
        }));
    }

    [Fact]
    public async Task SetCourseGroupAssignments_WhenCourseAssignmentsAlreadyActive_Throws()
    {
        var courseId = Guid.NewGuid();
        var instituteId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var repository = new Mock<ICourseStaffAssignmentRepository>();
        repository
            .Setup(repo => repo.GetCourseAssignments(courseId, true))
            .ReturnsAsync(new List<CourseStaffAssignmentResponse>
            {
                new()
                {
                    CourseStaffAssignmentId = Guid.NewGuid(),
                    CourseId = courseId,
                    InstituteId = instituteId,
                    AssignmentRoles = UserRoleType.SchoolTeacher,
                    StartDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true,
                    StaffUser = new InstituteStaffUserSummaryResponse { UserId = Guid.NewGuid() }
                }
            });

        var groupService = new Mock<ICourseEnrollmentGroupService>();
        groupService
            .Setup(service => service.GetCourseGroup(groupId))
            .ReturnsAsync(new CourseEnrollmentGroupResponse
            {
                CourseEnrollmentGroupId = groupId,
                CourseId = courseId,
                InstituteId = instituteId
            });

        var service = new CourseStaffAssignmentService(
            repository.Object,
            Mock.Of<ICourseService>(),
            groupService.Object,
            Mock.Of<IInstituteStaffAssignmentService>(),
            Mock.Of<IStudentCourseEnrollmentService>(),
            Mock.Of<IOtherContactsService>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetCourseGroupAssignments(new SetCourseGroupStaffAssignmentsRequest
        {
            CourseEnrollmentGroupId = groupId,
            CourseId = courseId,
            InstituteId = instituteId,
            StaffAssignments =
            [
                new CourseStaffAssignmentItemRequest
                {
                    UserId = Guid.NewGuid(),
                    AssignmentRoles = UserRoleType.Assistant,
                    StartDate = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true
                }
            ]
        }));
    }
}
