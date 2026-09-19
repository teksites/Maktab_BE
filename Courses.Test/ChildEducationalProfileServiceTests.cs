using Application.Users.Contracts;
using Application.Users.Implementation;
using Courses.Services;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Children;
using MaktabDataContracts.Responses.Children;
using Moq;
using Users.Repository;
using Users.Services;

namespace Courses.Test;

public class ChildEducationalProfileServiceTests
{
    [Fact]
    public async Task UpsertChildEducationalProfile_WhenNormalUserAndCatalogNotProvided_UpsertsProfile()
    {
        var userId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        IReadOnlyCollection<QuranSurah>? capturedSurahs = null;
        SurahCompletionStatus? capturedStatus = null;
        string? capturedRemarks = null;

        var repository = new Mock<IUserChildrenRepository>();
        repository
            .Setup(repo => repo.GetChild(childId))
            .ReturnsAsync(new Child
            {
                ChildId = childId,
                FamilyId = familyId,
                HasSurahCatalogBeenProvided = false
            });
        repository
            .Setup(repo => repo.UpsertChildEducationalProfile(childId, familyId, It.IsAny<IReadOnlyCollection<QuranSurah>>(), It.IsAny<SurahCompletionStatus>(), It.IsAny<string>()))
            .Callback<Guid, Guid, IReadOnlyCollection<QuranSurah>, SurahCompletionStatus, string>((_, _, surahs, status, remarks) =>
            {
                capturedSurahs = surahs;
                capturedStatus = status;
                capturedRemarks = remarks;
            })
            .ReturnsAsync(new ChildEducationalProfileResponse
            {
                ChildId = childId,
                FamilyId = familyId
            });

        var userService = new Mock<IUserService>();
        userService
            .Setup(service => service.GetUserInformation(userId))
            .ReturnsAsync(new MaktabDataContracts.Responses.Users.UserInformationResponse
            {
                UserId = userId,
                FamilyId = familyId
            });

        var dataAccess = new Mock<IDataAccessVerificationService>();
        dataAccess
            .Setup(service => service.HasElevatedAccess(UserRoleType.Normal))
            .Returns(false);

        var service = new ChildEducationalProfileService(
            repository.Object,
            userService.Object,
            dataAccess.Object,
            Mock.Of<ICourseStaffAssignmentService>(),
            Mock.Of<IStudentCourseEnrollmentService>());

        var result = await service.UpsertChildEducationalProfile(userId, UserRoleType.Normal, childId, new UpsertChildEducationalProfileRequest
        {
            FamilyId = familyId,
            CompletedSurahs = new List<QuranSurahSelectionRequest>
            {
                new() { Surah = QuranSurah.AlFatiha },
                new() { Surah = QuranSurah.AlIkhlas },
                new() { Surah = QuranSurah.AlFatiha }
            },
            SurahCompletionStatus = SurahCompletionStatus.PartiallyCompleted,
            Remarks = "Needs guided revision."
        });

        Assert.NotNull(result);
        Assert.NotNull(capturedSurahs);
        Assert.Equal(new[] { QuranSurah.AlFatiha, QuranSurah.AlIkhlas }, capturedSurahs!.ToArray());
        Assert.Equal(SurahCompletionStatus.PartiallyCompleted, capturedStatus);
        Assert.Equal("Needs guided revision.", capturedRemarks);
        repository.Verify(repo => repo.UpsertChildEducationalProfile(childId, familyId, It.IsAny<IReadOnlyCollection<QuranSurah>>(), It.IsAny<SurahCompletionStatus>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task UpsertChildEducationalProfile_WhenNormalUserAndCatalogAlreadyProvided_Throws()
    {
        var userId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var familyId = Guid.NewGuid();

        var repository = new Mock<IUserChildrenRepository>();
        repository
            .Setup(repo => repo.GetChild(childId))
            .ReturnsAsync(new Child
            {
                ChildId = childId,
                FamilyId = familyId,
                HasSurahCatalogBeenProvided = true
            });

        var userService = new Mock<IUserService>();
        userService
            .Setup(service => service.GetUserInformation(userId))
            .ReturnsAsync(new MaktabDataContracts.Responses.Users.UserInformationResponse
            {
                UserId = userId,
                FamilyId = familyId
            });

        var dataAccess = new Mock<IDataAccessVerificationService>();
        dataAccess
            .Setup(service => service.HasElevatedAccess(UserRoleType.Normal))
            .Returns(false);

        var service = new ChildEducationalProfileService(
            repository.Object,
            userService.Object,
            dataAccess.Object,
            Mock.Of<ICourseStaffAssignmentService>(),
            Mock.Of<IStudentCourseEnrollmentService>());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.UpsertChildEducationalProfile(userId, UserRoleType.Normal, childId, new UpsertChildEducationalProfileRequest
            {
                FamilyId = familyId,
                CompletedSurahs = new List<QuranSurahSelectionRequest>()
            }));

        repository.Verify(repo => repo.UpsertChildEducationalProfile(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<QuranSurah>>(), It.IsAny<SurahCompletionStatus>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpsertChildEducationalProfile_WhenElevatedUserAndCatalogAlreadyProvided_StillAllowsUpdate()
    {
        var userId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var familyId = Guid.NewGuid();

        var repository = new Mock<IUserChildrenRepository>();
        repository
            .Setup(repo => repo.GetChild(childId))
            .ReturnsAsync(new Child
            {
                ChildId = childId,
                FamilyId = familyId,
                HasSurahCatalogBeenProvided = true
            });
        repository
            .Setup(repo => repo.UpsertChildEducationalProfile(childId, familyId, It.IsAny<IReadOnlyCollection<QuranSurah>>(), It.IsAny<SurahCompletionStatus>(), It.IsAny<string>()))
            .ReturnsAsync(new ChildEducationalProfileResponse
            {
                ChildId = childId,
                FamilyId = familyId
            });

        var dataAccess = new Mock<IDataAccessVerificationService>();
        dataAccess
            .Setup(service => service.HasElevatedAccess(UserRoleType.SchoolAdmin))
            .Returns(true);

        var service = new ChildEducationalProfileService(
            repository.Object,
            Mock.Of<IUserService>(),
            dataAccess.Object,
            Mock.Of<ICourseStaffAssignmentService>(),
            Mock.Of<IStudentCourseEnrollmentService>());

        var result = await service.UpsertChildEducationalProfile(userId, UserRoleType.SchoolAdmin, childId, new UpsertChildEducationalProfileRequest
        {
            FamilyId = familyId,
            CompletedSurahs = new List<QuranSurahSelectionRequest>
            {
                new() { Surah = QuranSurah.AnNas }
            }
        });

        Assert.NotNull(result);
        repository.Verify(repo => repo.UpsertChildEducationalProfile(childId, familyId, It.IsAny<IReadOnlyCollection<QuranSurah>>(), It.IsAny<SurahCompletionStatus>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task UpsertChildEducationalProfile_WhenRemarksExceedLimit_RejectsBeforeRepositoryWrite()
    {
        var userId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var repository = new Mock<IUserChildrenRepository>();
        repository.Setup(repo => repo.GetChild(childId)).ReturnsAsync(new Child { ChildId = childId, FamilyId = familyId });

        var userService = new Mock<IUserService>();
        userService.Setup(service => service.GetUserInformation(userId)).ReturnsAsync(new MaktabDataContracts.Responses.Users.UserInformationResponse { UserId = userId, FamilyId = familyId });
        var dataAccess = new Mock<IDataAccessVerificationService>();
        dataAccess.Setup(service => service.HasElevatedAccess(UserRoleType.Normal)).Returns(false);

        var service = new ChildEducationalProfileService(repository.Object, userService.Object, dataAccess.Object, Mock.Of<ICourseStaffAssignmentService>(), Mock.Of<IStudentCourseEnrollmentService>());

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpsertChildEducationalProfile(userId, UserRoleType.Normal, childId, new UpsertChildEducationalProfileRequest
        {
            FamilyId = familyId,
            Remarks = new string('a', 501)
        }));

        repository.Verify(repo => repo.UpsertChildEducationalProfile(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<QuranSurah>>(), It.IsAny<SurahCompletionStatus>(), It.IsAny<string>()), Times.Never);
    }
}
