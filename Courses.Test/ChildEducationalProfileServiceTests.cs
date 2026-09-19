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
    public async Task UpsertChildEducationalProfile_WhenNormalUserAndCatalogNotProvided_StoresEachSurahAssessment()
    {
        var userId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        IReadOnlyCollection<QuranSurahAssessmentRequest>? capturedAssessments = null;
        var repository = CreateChildRepository(childId, familyId, false);
        repository.Setup(repo => repo.UpsertChildEducationalProfile(childId, familyId, It.IsAny<IReadOnlyCollection<QuranSurahAssessmentRequest>>()))
            .Callback<Guid, Guid, IReadOnlyCollection<QuranSurahAssessmentRequest>>((_, _, assessments) => capturedAssessments = assessments)
            .ReturnsAsync(new ChildEducationalProfileResponse { ChildId = childId, FamilyId = familyId });

        var service = CreateService(repository, CreateFamilyUserService(userId, familyId), CreateNormalDataAccess());
        var result = await service.UpsertChildEducationalProfile(userId, UserRoleType.Normal, childId, new UpsertChildEducationalProfileRequest
        {
            FamilyId = familyId,
            SurahAssessments = new List<QuranSurahAssessmentRequest>
            {
                new() { Surah = QuranSurah.AlFatiha, CompletionStatus = SurahCompletionStatus.Completed, Remarks = "Memorized." },
                new() { Surah = QuranSurah.AlIkhlas, CompletionStatus = SurahCompletionStatus.PartiallyCompleted, Remarks = "Needs revision." }
            }
        });

        Assert.NotNull(result);
        Assert.NotNull(capturedAssessments);
        Assert.Equal(2, capturedAssessments!.Count);
        Assert.Equal(SurahCompletionStatus.Completed, capturedAssessments.Single(item => item.Surah == QuranSurah.AlFatiha).CompletionStatus);
        Assert.Equal("Needs revision.", capturedAssessments.Single(item => item.Surah == QuranSurah.AlIkhlas).Remarks);
    }

    [Fact]
    public async Task UpsertChildEducationalProfile_WhenSameSurahIsSubmittedTwice_RejectsBeforeRepositoryWrite()
    {
        var userId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var repository = CreateChildRepository(childId, familyId, false);
        var service = CreateService(repository, CreateFamilyUserService(userId, familyId), CreateNormalDataAccess());

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpsertChildEducationalProfile(userId, UserRoleType.Normal, childId, new UpsertChildEducationalProfileRequest
        {
            FamilyId = familyId,
            SurahAssessments = new List<QuranSurahAssessmentRequest>
            {
                new() { Surah = QuranSurah.AlFatiha, CompletionStatus = SurahCompletionStatus.Completed },
                new() { Surah = QuranSurah.AlFatiha, CompletionStatus = SurahCompletionStatus.Incomplete }
            }
        }));

        repository.Verify(repo => repo.UpsertChildEducationalProfile(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<QuranSurahAssessmentRequest>>()), Times.Never);
    }

    [Fact]
    public async Task UpsertChildEducationalProfile_WhenNormalUserAndCatalogAlreadyProvided_Throws()
    {
        var userId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var repository = CreateChildRepository(childId, familyId, true);
        var service = CreateService(repository, CreateFamilyUserService(userId, familyId), CreateNormalDataAccess());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpsertChildEducationalProfile(userId, UserRoleType.Normal, childId, new UpsertChildEducationalProfileRequest
        {
            FamilyId = familyId
        }));
    }

    [Fact]
    public async Task UpsertChildEducationalProfile_WhenElevatedUserAndCatalogAlreadyProvided_StillAllowsUpdate()
    {
        var userId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var repository = CreateChildRepository(childId, familyId, true);
        repository.Setup(repo => repo.UpsertChildEducationalProfile(childId, familyId, It.IsAny<IReadOnlyCollection<QuranSurahAssessmentRequest>>()))
            .ReturnsAsync(new ChildEducationalProfileResponse { ChildId = childId, FamilyId = familyId });
        var dataAccess = new Mock<IDataAccessVerificationService>();
        dataAccess.Setup(service => service.HasElevatedAccess(UserRoleType.SchoolAdmin)).Returns(true);
        var service = CreateService(repository, new Mock<IUserService>(), dataAccess);

        var result = await service.UpsertChildEducationalProfile(userId, UserRoleType.SchoolAdmin, childId, new UpsertChildEducationalProfileRequest
        {
            FamilyId = familyId,
            SurahAssessments = new List<QuranSurahAssessmentRequest>
            {
                new() { Surah = QuranSurah.AnNas, CompletionStatus = SurahCompletionStatus.Incomplete, Remarks = "Assessment pending." }
            }
        });

        Assert.NotNull(result);
        repository.Verify(repo => repo.UpsertChildEducationalProfile(childId, familyId, It.IsAny<IReadOnlyCollection<QuranSurahAssessmentRequest>>()), Times.Once);
    }

    [Fact]
    public async Task UpsertChildEducationalProfile_WhenOneSurahRemarkExceedsLimit_RejectsBeforeRepositoryWrite()
    {
        var userId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var repository = CreateChildRepository(childId, familyId, false);
        var service = CreateService(repository, CreateFamilyUserService(userId, familyId), CreateNormalDataAccess());

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpsertChildEducationalProfile(userId, UserRoleType.Normal, childId, new UpsertChildEducationalProfileRequest
        {
            FamilyId = familyId,
            SurahAssessments = new List<QuranSurahAssessmentRequest>
            {
                new() { Surah = QuranSurah.AlFatiha, Remarks = new string('a', 501) }
            }
        }));

        repository.Verify(repo => repo.UpsertChildEducationalProfile(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<QuranSurahAssessmentRequest>>()), Times.Never);
    }

    private static ChildEducationalProfileService CreateService(Mock<IUserChildrenRepository> repository, Mock<IUserService> userService, Mock<IDataAccessVerificationService> dataAccess)
        => new(repository.Object, userService.Object, dataAccess.Object, Mock.Of<ICourseStaffAssignmentService>(), Mock.Of<IStudentCourseEnrollmentService>());

    private static Mock<IUserChildrenRepository> CreateChildRepository(Guid childId, Guid familyId, bool catalogProvided)
    {
        var repository = new Mock<IUserChildrenRepository>();
        repository.Setup(repo => repo.GetChild(childId)).ReturnsAsync(new Child { ChildId = childId, FamilyId = familyId, HasSurahCatalogBeenProvided = catalogProvided });
        return repository;
    }

    private static Mock<IUserService> CreateFamilyUserService(Guid userId, Guid familyId)
    {
        var userService = new Mock<IUserService>();
        userService.Setup(service => service.GetUserInformation(userId)).ReturnsAsync(new MaktabDataContracts.Responses.Users.UserInformationResponse { UserId = userId, FamilyId = familyId });
        return userService;
    }

    private static Mock<IDataAccessVerificationService> CreateNormalDataAccess()
    {
        var dataAccess = new Mock<IDataAccessVerificationService>();
        dataAccess.Setup(service => service.HasElevatedAccess(UserRoleType.Normal)).Returns(false);
        return dataAccess;
    }
}
