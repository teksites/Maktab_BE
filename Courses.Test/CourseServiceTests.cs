using Courses.Implementation.Services;
using Courses.Repository;
using Courses.Services;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using Moq;

namespace Courses.Test;

public class CourseServiceTests
{
    [Fact]
    public async Task UpdateCourseEnrollmentGroup_MapsNewFields()
    {
        var groupId = Guid.NewGuid();
        AddCourseEnrollmentGroup? capturedRequest = null;

        var repository = new Mock<ICourseRepository>();
        var groupService = new Mock<ICourseEnrollmentGroupService>();
        groupService
            .Setup(service => service.UpdateCourseEnrollmentGroup(groupId, It.IsAny<AddCourseEnrollmentGroup>()))
            .Callback<Guid, AddCourseEnrollmentGroup>((_, request) => capturedRequest = request)
            .ReturnsAsync(true);
        groupService
            .Setup(service => service.GetGroup(groupId))
            .ReturnsAsync(new CourseEnrollmentGroupResponse
            {
                CourseEnrollmentGroupId = groupId
            });

        var service = new CourseService(repository.Object, groupService.Object);

        await service.UpdateCourseEnrollmentGroup(new UpdateCourseEnrollmentGroup
        {
            CourseEnrollmentGroupId = groupId,
            CourseId = Guid.NewGuid(),
            InstituteId = Guid.NewGuid(),
            GroupTitle = "Group A",
            GroupTitleFr = "Groupe A",
            Details = "details",
            DetailsFr = "details fr",
            IsActive = true,
            MaxStudents = 10,
            Fee = 200,
            IfRegistrationOpen = true,
            DayCareFee = 25,
            GroupIndex = 2,
            MinAge = 5,
            MaxAge = 12,
            IsCourseGroupHasPrequisite = true,
            AcedemicGroups = new List<AcedemicGroupType> { AcedemicGroupType.None }
        });

        Assert.NotNull(capturedRequest);
        Assert.Equal(5, capturedRequest!.MinAge);
        Assert.Equal(12, capturedRequest.MaxAge);
        Assert.True(capturedRequest.IsCourseGroupHasPrequisite);
    }

    [Fact]
    public async Task SetCourseRegistrationOpenStatus_PreservesManualEnrollmentPrerequisiteAndEventFlags()
    {
        var courseId = Guid.NewGuid();
        AddCourse? capturedRequest = null;

        var repository = new Mock<ICourseRepository>();
        repository
            .Setup(repo => repo.GetCourse(courseId))
            .ReturnsAsync(new CourseResponseDetailed
            {
                CourseId = courseId,
                InstituteId = Guid.NewGuid(),
                Name = "Course",
                NameFr = "Cours",
                Description = "Desc",
                DescriptionFr = "Desc fr",
                Details = "Details",
                DetailsFr = "Details fr",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(30),
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                UpdatedOn = DateTime.UtcNow.AddDays(-1),
                CanSelectMultipleEnrollmentGroups = false,
                PolicyHyperLink = string.Empty,
                IsCourseCompleted = false,
                IsCourseHasPrequisite = true,
                IsAdultRestricted = true,
                CustomRequirements = new List<CourseCustomRequirements>
                {
                    CourseCustomRequirements.SurahRequirement
                },
                IsCourseAnEvent = true,
                IsManualEnrollment = true,
                IsRegistrationOpened = true,
                OfferDaycare = true,
                RegistrationStartDate = DateTime.UtcNow.AddDays(-5),
                RegistrationEndDate = DateTime.UtcNow.AddDays(5),
                CourseSession = CourseSessionType.Fall,
                RegistrationFee = 100
            });
        repository
            .Setup(repo => repo.UpdateCourse(courseId, It.IsAny<AddCourse>()))
            .Callback<Guid, AddCourse>((_, request) => capturedRequest = request)
            .ReturnsAsync(true);

        var service = new CourseService(repository.Object, Mock.Of<ICourseEnrollmentGroupService>());

        var result = await service.SetCourseRegistrationOpenStatus(courseId, false);

        Assert.True(result);
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest!.IsManualEnrollment);
        Assert.True(capturedRequest.IsCourseHasPrequisite);
        Assert.True(capturedRequest.IsAdultRestricted);
        Assert.Equal(new[] { CourseCustomRequirements.SurahRequirement }, capturedRequest.CustomRequirements);
        Assert.True(capturedRequest.IsCourseAnEvent);
        Assert.False(capturedRequest.IsRegistrationOpened);
    }
}
