using Courses.Implementation.Services;
using Courses.Repository;
using Courses.Services;
using Email;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Attendance;
using MaktabDataContracts.Responses.Attendance;
using MaktabDataContracts.Responses.Users;
using Moq;
using Users.Services;

namespace Courses.Test;

public class StudentCourseAttendanceServiceTests
{
    [Fact]
    public async Task GetCourseGroupAttendance_WhenNothingSaved_ReturnsRosterDefaults()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var instituteId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var attendanceDate = new DateTime(2026, 8, 2, 14, 30, 0, DateTimeKind.Local);

        var repository = new Mock<IStudentCourseAttendanceRepository>();
        repository
            .Setup(repo => repo.GetCourseGroupAttendance(groupId, new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc), null))
            .ReturnsAsync(new List<StudentCourseAttendanceResponse>());

        var courseStaffAssignmentService = new Mock<ICourseStaffAssignmentService>();
        courseStaffAssignmentService
            .Setup(service => service.GetAssignedCourseGroupRoster(userId, UserRoleType.SchoolTeacher, groupId))
            .ReturnsAsync(CreateRoster(courseId, groupId, instituteId, enrollmentId, childId, familyId));

        var service = CreateAttendanceService(repository, courseStaffAssignmentService);

        var result = await service.GetCourseGroupAttendance(userId, UserRoleType.SchoolTeacher, new GetCourseGroupAttendanceRequest
        {
            CourseEnrollmentGroupId = groupId,
            AttendanceDate = attendanceDate
        });

        Assert.Equal(courseId, result.CourseId);
        Assert.Equal(groupId, result.CourseEnrollmentGroupId);
        Assert.Equal(instituteId, result.InstituteId);
        Assert.Equal(new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc), result.AttendanceDate);
        Assert.Equal(userId, result.RecordedByUserId);
        Assert.Single(result.Students);
        Assert.Equal(enrollmentId, result.Students[0].StudentCourseEnrollmentId);
        Assert.Equal(childId, result.Students[0].ChildId);
        Assert.Equal(familyId, result.Students[0].FamilyId);
        Assert.Equal(AttendanceStatus.Unknown, result.Students[0].AttendanceStatus);
        Assert.Equal(PickupContactType.Unknown, result.Students[0].PickupContactType);
        Assert.False(result.Students[0].IsActive);
    }

    [Fact]
    public async Task UpsertCourseGroupAttendance_WhenPickupReferenceHasNoTime_Throws()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var instituteId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var motherId = Guid.NewGuid();

        var repository = new Mock<IStudentCourseAttendanceRepository>(MockBehavior.Strict);
        repository
            .Setup(repo => repo.GetCourseGroupAttendance(groupId, It.IsAny<DateTime>(), null))
            .ReturnsAsync(new List<StudentCourseAttendanceResponse>());
        var courseStaffAssignmentService = new Mock<ICourseStaffAssignmentService>();
        courseStaffAssignmentService
            .Setup(service => service.GetAssignedCourseGroupRoster(userId, UserRoleType.SchoolTeacher, groupId))
            .ReturnsAsync(CreateRoster(courseId, groupId, instituteId, enrollmentId, childId, familyId, motherId));

        var service = CreateAttendanceService(repository, courseStaffAssignmentService);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpsertCourseGroupAttendance(
            userId,
            UserRoleType.SchoolTeacher,
            new UpsertCourseGroupAttendanceRequest
            {
                CourseId = courseId,
                CourseEnrollmentGroupId = groupId,
                InstituteId = instituteId,
                AttendanceDate = new DateTime(2026, 8, 2, 10, 0, 0, DateTimeKind.Local),
                Students = new List<UpsertStudentCourseAttendanceRequest>
                {
                    new()
                    {
                        StudentCourseEnrollmentId = enrollmentId,
                        ChildId = childId,
                        FamilyId = familyId,
                        AttendanceStatus = AttendanceStatus.Present,
                        PickupContactType = PickupContactType.Mother,
                        PickupUserId = motherId
                    }
                }
            }));

        Assert.Equal("Pickup contact information requires an early pickup time.", exception.Message);
    }

    [Fact]
    public async Task UpsertCourseGroupAttendance_WhenValid_PersistsAndReturnsSavedAttendance()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var instituteId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var motherId = Guid.NewGuid();
        var capturedRequest = default(UpsertCourseGroupAttendanceRequest);
        var normalizedAttendanceDate = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc);
        var earlyPickupTime = new DateTime(2026, 8, 2, 16, 15, 0, DateTimeKind.Local);

        var repository = new Mock<IStudentCourseAttendanceRepository>();
        repository
            .Setup(repo => repo.UpsertCourseGroupAttendance(It.IsAny<UpsertCourseGroupAttendanceRequest>()))
            .Callback<UpsertCourseGroupAttendanceRequest>(request => capturedRequest = request)
            .ReturnsAsync(new CourseGroupAttendanceResponse());
        repository
            .Setup(repo => repo.GetCourseGroupAttendance(groupId, normalizedAttendanceDate, null))
            .ReturnsAsync(new List<StudentCourseAttendanceResponse>
            {
                new()
                {
                    StudentCourseAttendanceId = Guid.NewGuid(),
                    StudentCourseEnrollmentId = enrollmentId,
                    ChildId = childId,
                    FamilyId = familyId,
                    ChildName = "Student One",
                    AttendanceStatus = AttendanceStatus.Present,
                    EarlyPickupTime = new DateTime(2026, 8, 2, 16, 15, 0, DateTimeKind.Utc),
                    PickupContactType = PickupContactType.Mother,
                    PickupUserId = motherId,
                    PickupDisplayName = "Mother One",
                    Notes = "Picked up early",
                    IsActive = true,
                    RecordedByUserId = userId,
                    CreatedAt = normalizedAttendanceDate,
                    UpdatedOn = normalizedAttendanceDate
                }
            });

        var courseStaffAssignmentService = new Mock<ICourseStaffAssignmentService>();
        courseStaffAssignmentService
            .Setup(service => service.GetAssignedCourseGroupRoster(userId, UserRoleType.SchoolTeacher, groupId))
            .ReturnsAsync(CreateRoster(courseId, groupId, instituteId, enrollmentId, childId, familyId, motherId));

        var service = CreateAttendanceService(repository, courseStaffAssignmentService);

        var result = await service.UpsertCourseGroupAttendance(userId, UserRoleType.SchoolTeacher, new UpsertCourseGroupAttendanceRequest
        {
            CourseId = courseId,
            CourseEnrollmentGroupId = groupId,
            InstituteId = instituteId,
            AttendanceDate = new DateTime(2026, 8, 2, 9, 45, 0, DateTimeKind.Local),
            Students = new List<UpsertStudentCourseAttendanceRequest>
            {
                new()
                {
                    StudentCourseEnrollmentId = enrollmentId,
                    ChildId = childId,
                    FamilyId = familyId,
                    AttendanceStatus = AttendanceStatus.Present,
                    EarlyPickupTime = earlyPickupTime,
                    PickupContactType = PickupContactType.Mother,
                    PickupUserId = motherId,
                    Notes = "Picked up early"
                }
            }
        });

        Assert.NotNull(capturedRequest);
        Assert.Equal(userId, capturedRequest!.RecordedByUserId);
        Assert.Equal(normalizedAttendanceDate, capturedRequest.AttendanceDate);
        Assert.Single(capturedRequest.Students);
        Assert.Equal(new DateTime(2026, 8, 2, 16, 15, 0, DateTimeKind.Utc), capturedRequest.Students[0].EarlyPickupTime);
        Assert.Single(result.Students);
        Assert.Equal(AttendanceStatus.Present, result.Students[0].AttendanceStatus);
        Assert.Equal(PickupContactType.Mother, result.Students[0].PickupContactType);
        Assert.Equal(userId, result.RecordedByUserId);
    }

    [Fact]
    public async Task GetStaffAttendanceRecords_WhenTeacherRequestsUnassignedGroup_Throws()
    {
        var userId = Guid.NewGuid();
        var assignedCourseId = Guid.NewGuid();
        var assignedGroupId = Guid.NewGuid();
        var requestedGroupId = Guid.NewGuid();

        var repository = new Mock<IStudentCourseAttendanceRepository>(MockBehavior.Strict);
        var courseStaffAssignmentService = new Mock<ICourseStaffAssignmentService>();
        courseStaffAssignmentService
            .Setup(service => service.GetAssignedCourseGroups(userId, false))
            .ReturnsAsync(new List<TeacherAssignedCourseGroupResponse>
            {
                new()
                {
                    CourseId = assignedCourseId,
                    CourseEnrollmentGroupId = assignedGroupId
                }
            });

        var service = CreateAttendanceService(repository, courseStaffAssignmentService);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetStaffAttendanceRecords(
            userId,
            UserRoleType.SchoolTeacher,
            new GetAttendanceRecordsRequest
            {
                CourseEnrollmentGroupId = requestedGroupId
            }));

        Assert.Equal("You are not assigned to the selected course group.", exception.Message);
    }

    [Fact]
    public async Task GetStaffAttendanceReport_WhenGroupedByCourse_ReturnsAggregatedCounts()
    {
        var userId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var instituteId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var childOneId = Guid.NewGuid();
        var childTwoId = Guid.NewGuid();
        var attendanceDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        var repository = new Mock<IStudentCourseAttendanceRepository>();
        repository
            .Setup(repo => repo.GetAttendanceRecords(
                It.IsAny<GetAttendanceRecordsRequest>(),
                null))
            .ReturnsAsync(new List<AttendanceRecordResponse>
            {
                new()
                {
                    StudentCourseAttendanceId = Guid.NewGuid(),
                    StudentCourseEnrollmentId = Guid.NewGuid(),
                    CourseEnrollmentGroupId = groupId,
                    CourseId = courseId,
                    InstituteId = instituteId,
                    FamilyId = familyId,
                    ChildId = childOneId,
                    ChildName = "Student One",
                    CourseName = "Quran",
                    GroupTitle = "Group A",
                    AttendanceDate = attendanceDate,
                    AttendanceStatus = AttendanceStatus.Present
                },
                new()
                {
                    StudentCourseAttendanceId = Guid.NewGuid(),
                    StudentCourseEnrollmentId = Guid.NewGuid(),
                    CourseEnrollmentGroupId = groupId,
                    CourseId = courseId,
                    InstituteId = instituteId,
                    FamilyId = familyId,
                    ChildId = childTwoId,
                    ChildName = "Student Two",
                    CourseName = "Quran",
                    GroupTitle = "Group A",
                    AttendanceDate = attendanceDate,
                    AttendanceStatus = AttendanceStatus.Present,
                    LateArrivalTime = attendanceDate.AddHours(1)
                },
                new()
                {
                    StudentCourseAttendanceId = Guid.NewGuid(),
                    StudentCourseEnrollmentId = Guid.NewGuid(),
                    CourseEnrollmentGroupId = groupId,
                    CourseId = courseId,
                    InstituteId = instituteId,
                    FamilyId = familyId,
                    ChildId = Guid.NewGuid(),
                    ChildName = "Student Three",
                    CourseName = "Quran",
                    GroupTitle = "Group A",
                    AttendanceDate = attendanceDate,
                    AttendanceStatus = AttendanceStatus.Absent
                }
            });

        var courseStaffAssignmentService = new Mock<ICourseStaffAssignmentService>(MockBehavior.Strict);
        var service = CreateAttendanceService(repository, courseStaffAssignmentService);

        var result = await service.GetStaffAttendanceReport(
            userId,
            UserRoleType.SchoolAdmin,
            new GetAttendanceReportRequest
            {
                Grouping = AttendanceReportGroupingType.Course
            });

        Assert.Single(result.Items);
        Assert.Equal(courseId, result.Items[0].CourseId);
        Assert.Equal("Quran", result.Items[0].CourseName);
        Assert.Equal(3, result.Items[0].TotalRecords);
        Assert.Equal(2, result.Items[0].PresentCount);
        Assert.Equal(1, result.Items[0].AbsentCount);
        Assert.Equal(1, result.Items[0].LateArrivalCount);
        Assert.Equal(0, result.Items[0].EarlyDepartureCount);
        Assert.Equal(1, result.Items[0].OnTimePresenceCount);
    }

    [Fact]
    public async Task GetFamilyAttendanceReport_WhenGroupedByDaily_ReturnsFamilySummary()
    {
        var familyId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var instituteId = Guid.NewGuid();
        var attendanceDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        var repository = new Mock<IStudentCourseAttendanceRepository>();
        repository
            .Setup(repo => repo.GetAttendanceRecords(
                It.Is<GetAttendanceRecordsRequest>(request => request.FamilyId == familyId),
                null))
            .ReturnsAsync(new List<AttendanceRecordResponse>
            {
                new()
                {
                    StudentCourseAttendanceId = Guid.NewGuid(),
                    StudentCourseEnrollmentId = Guid.NewGuid(),
                    CourseEnrollmentGroupId = groupId,
                    CourseId = courseId,
                    InstituteId = instituteId,
                    FamilyId = familyId,
                    ChildId = Guid.NewGuid(),
                    ChildName = "Student One",
                    CourseName = "Quran",
                    GroupTitle = "Group A",
                    AttendanceDate = attendanceDate,
                    AttendanceStatus = AttendanceStatus.Present
                },
                new()
                {
                    StudentCourseAttendanceId = Guid.NewGuid(),
                    StudentCourseEnrollmentId = Guid.NewGuid(),
                    CourseEnrollmentGroupId = groupId,
                    CourseId = courseId,
                    InstituteId = instituteId,
                    FamilyId = familyId,
                    ChildId = Guid.NewGuid(),
                    ChildName = "Student Two",
                    CourseName = "Quran",
                    GroupTitle = "Group A",
                    AttendanceDate = attendanceDate,
                    AttendanceStatus = AttendanceStatus.Absent
                }
            });

        var courseStaffAssignmentService = new Mock<ICourseStaffAssignmentService>(MockBehavior.Strict);
        var service = CreateAttendanceService(repository, courseStaffAssignmentService);

        var result = await service.GetFamilyAttendanceReport(
            familyId,
            new GetAttendanceReportRequest
            {
                Grouping = AttendanceReportGroupingType.Daily
            });

        Assert.Single(result.Items);
        Assert.Equal("2026-08-01", result.Items[0].GroupKey);
        Assert.Equal(2, result.Items[0].TotalRecords);
        Assert.Equal(1, result.Items[0].PresentCount);
        Assert.Equal(1, result.Items[0].AbsentCount);
    }

    [Fact]
    public async Task UpsertCourseGroupAttendance_WhenAttendanceBecomesAbsent_SendsBilingualEmailToFamily()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var instituteId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        MultiUserEmailData? sentEmail = null;

        var repository = new Mock<IStudentCourseAttendanceRepository>();
        repository
            .Setup(repo => repo.GetCourseGroupAttendance(groupId, It.IsAny<DateTime>(), null))
            .ReturnsAsync(new List<StudentCourseAttendanceResponse>());
        repository
            .Setup(repo => repo.UpsertCourseGroupAttendance(It.IsAny<UpsertCourseGroupAttendanceRequest>()))
            .ReturnsAsync(new CourseGroupAttendanceResponse());

        var courseStaffAssignmentService = new Mock<ICourseStaffAssignmentService>();
        courseStaffAssignmentService
            .Setup(service => service.GetAssignedCourseGroupRoster(userId, UserRoleType.SchoolTeacher, groupId))
            .ReturnsAsync(CreateRoster(courseId, groupId, instituteId, enrollmentId, childId, familyId));

        var sendEmailService = new Mock<ISendEmailService>();
        sendEmailService
            .Setup(service => service.SendBulkEmail(It.IsAny<MultiUserEmailData>()))
            .Callback<MultiUserEmailData>(email => sentEmail = email)
            .ReturnsAsync(true);

        var userService = CreateUserService(familyId);
        var service = CreateAttendanceService(repository, courseStaffAssignmentService, sendEmailService, userService);

        await service.UpsertCourseGroupAttendance(userId, UserRoleType.SchoolTeacher, new UpsertCourseGroupAttendanceRequest
        {
            CourseId = courseId,
            CourseEnrollmentGroupId = groupId,
            InstituteId = instituteId,
            AttendanceDate = new DateTime(2026, 8, 2, 9, 45, 0, DateTimeKind.Utc),
            Students = new List<UpsertStudentCourseAttendanceRequest>
            {
                new()
                {
                    StudentCourseEnrollmentId = enrollmentId,
                    ChildId = childId,
                    FamilyId = familyId,
                    AttendanceStatus = AttendanceStatus.Absent,
                    Notes = "No show"
                }
            }
        });

        Assert.NotNull(sentEmail);
        Assert.Equal(2, sentEmail!.To.Count());
        Assert.Contains("student one", sentEmail.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("absent", sentEmail.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cher parent", sentEmail.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dear parent", sentEmail.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpsertCourseGroupAttendance_WhenAttendanceStateIsUnchanged_DoesNotResendEmail()
    {
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var instituteId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var attendanceDate = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc);

        var repository = new Mock<IStudentCourseAttendanceRepository>();
        repository
            .Setup(repo => repo.GetCourseGroupAttendance(groupId, attendanceDate, null))
            .ReturnsAsync(new List<StudentCourseAttendanceResponse>
            {
                new()
                {
                    StudentCourseAttendanceId = Guid.NewGuid(),
                    StudentCourseEnrollmentId = enrollmentId,
                    ChildId = childId,
                    FamilyId = familyId,
                    ChildName = "Student One",
                    AttendanceStatus = AttendanceStatus.Absent,
                    Notes = "Already absent",
                    IsActive = true
                }
            });
        repository
            .Setup(repo => repo.UpsertCourseGroupAttendance(It.IsAny<UpsertCourseGroupAttendanceRequest>()))
            .ReturnsAsync(new CourseGroupAttendanceResponse());

        var courseStaffAssignmentService = new Mock<ICourseStaffAssignmentService>();
        courseStaffAssignmentService
            .Setup(service => service.GetAssignedCourseGroupRoster(userId, UserRoleType.SchoolTeacher, groupId))
            .ReturnsAsync(CreateRoster(courseId, groupId, instituteId, enrollmentId, childId, familyId));

        var sendEmailService = new Mock<ISendEmailService>(MockBehavior.Strict);
        var service = CreateAttendanceService(repository, courseStaffAssignmentService, sendEmailService, CreateUserService(familyId));

        await service.UpsertCourseGroupAttendance(userId, UserRoleType.SchoolTeacher, new UpsertCourseGroupAttendanceRequest
        {
            CourseId = courseId,
            CourseEnrollmentGroupId = groupId,
            InstituteId = instituteId,
            AttendanceDate = attendanceDate,
            Students = new List<UpsertStudentCourseAttendanceRequest>
            {
                new()
                {
                    StudentCourseEnrollmentId = enrollmentId,
                    ChildId = childId,
                    FamilyId = familyId,
                    AttendanceStatus = AttendanceStatus.Absent
                }
            }
        });

        sendEmailService.Verify(service => service.SendBulkEmail(It.IsAny<MultiUserEmailData>()), Times.Never);
    }

    private static TeacherCourseGroupRosterResponse CreateRoster(
        Guid courseId,
        Guid groupId,
        Guid instituteId,
        Guid enrollmentId,
        Guid childId,
        Guid familyId,
        Guid? motherId = null)
    {
        return new TeacherCourseGroupRosterResponse
        {
            CourseId = courseId,
            CourseEnrollmentGroupId = groupId,
            InstituteId = instituteId,
            Students = new List<TeacherCourseGroupStudentResponse>
            {
                new()
                {
                    StudentCourseEnrollmentId = enrollmentId,
                    ChildId = childId,
                    FamilyId = familyId,
                    ChildName = "Student One",
                    PickupContacts = motherId.HasValue
                        ? new List<AttendancePickupContactResponse>
                        {
                            new()
                            {
                                PickupContactType = PickupContactType.Mother,
                                PickupUserId = motherId.Value,
                                DisplayName = "Mother One"
                            }
                        }
                        : new List<AttendancePickupContactResponse>()
                }
            }
        };
    }

    private static Mock<IUserService> CreateUserService(Guid familyId)
    {
        var userService = new Mock<IUserService>();
        userService
            .Setup(service => service.GetAllFamilyUsersInformation(familyId, true))
            .ReturnsAsync(new[]
            {
                new UserInformationResponse
                {
                    FamilyId = familyId,
                    Email = "mother@example.com",
                    Relationship = Relationship.Mother
                },
                new UserInformationResponse
                {
                    FamilyId = familyId,
                    Email = "father@example.com",
                    Relationship = Relationship.Father
                }
            });

        return userService;
    }

    private static StudentCourseAttendanceService CreateAttendanceService(
        Mock<IStudentCourseAttendanceRepository>? repository = null,
        Mock<ICourseStaffAssignmentService>? courseStaffAssignmentService = null,
        Mock<ISendEmailService>? sendEmailService = null,
        Mock<IUserService>? userService = null)
    {
        return new StudentCourseAttendanceService(
            (repository ?? new Mock<IStudentCourseAttendanceRepository>()).Object,
            (courseStaffAssignmentService ?? new Mock<ICourseStaffAssignmentService>()).Object,
            (sendEmailService ?? new Mock<ISendEmailService>()).Object,
            (userService ?? CreateUserService(Guid.NewGuid())).Object);
    }
}
