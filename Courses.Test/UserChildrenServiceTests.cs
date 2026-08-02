using Application.Users.Contracts;
using Application.Users.Implementation;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Children;
using Microsoft.Extensions.Configuration;
using Moq;
using Users.Repository;

namespace Courses.Test;

public class UserChildrenServiceTests
{
    [Fact]
    public async Task AddChild_DefaultsUserTypeToChild()
    {
        Child? capturedChild = null;
        var repository = new Mock<IUserChildrenRepository>();
        repository
            .Setup(repo => repo.AddChild(It.IsAny<Child>()))
            .Callback<Child>(child => capturedChild = child)
            .ReturnsAsync((Child child) => child);

        var service = new UserChildrenService(Mock.Of<IConfiguration>(), repository.Object);

        var result = await service.AddChild(new AddChildRequest
        {
            FamilyId = Guid.NewGuid(),
            FirstName = "Default",
            LastName = "Child",
            DateOfBirth = DateTime.UtcNow.AddYears(-8),
            Gender = Gender.Unknown,
            RAMQExpiry = DateTime.UtcNow.AddYears(1),
            RAMQNumber = "DEFAULT123",
            RAMQSequenceNumber = 1,
            HasAllergy = false,
            Allergies = string.Empty,
            OtherHealthConditions = string.Empty,
            AcedemicGroup = AcedemicGroupType.None,
            Consent = "yes"
        });

        Assert.NotNull(capturedChild);
        Assert.Equal(UserType.Child, capturedChild!.UserType);
        Assert.NotNull(result);
        Assert.Equal(UserType.Child, result!.Result.UserType);
    }

    [Fact]
    public async Task AddChild_MapsUserTypeIntoRepositoryAndResponse()
    {
        Child? capturedChild = null;
        var repository = new Mock<IUserChildrenRepository>();
        repository
            .Setup(repo => repo.AddChild(It.IsAny<Child>()))
            .Callback<Child>(child => capturedChild = child)
            .ReturnsAsync((Child child) => child);

        var service = new UserChildrenService(Mock.Of<IConfiguration>(), repository.Object);

        var result = await service.AddChild(new AddChildRequest
        {
            FamilyId = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "Child",
            DateOfBirth = DateTime.UtcNow.AddYears(-10),
            Gender = Gender.Male,
            RAMQExpiry = DateTime.UtcNow.AddYears(1),
            RAMQNumber = "ABC123",
            RAMQSequenceNumber = 1,
            HasAllergy = false,
            Allergies = string.Empty,
            OtherHealthConditions = string.Empty,
            AcedemicGroup = AcedemicGroupType.None,
            Consent = "yes",
            UserType = UserType.Mother
        });

        Assert.NotNull(capturedChild);
        Assert.Equal(UserType.Mother, capturedChild!.UserType);
        Assert.NotNull(result);
        Assert.Equal(UserType.Mother, result!.Result.UserType);
    }

    [Fact]
    public async Task GetUserChilds_DefaultsToChildMotherFatherAndGuardian()
    {
        var familyId = Guid.NewGuid();
        var repository = new Mock<IUserChildrenRepository>();
        repository
            .Setup(repo => repo.GetFamilyChildren(familyId))
            .ReturnsAsync(new[]
            {
                CreateChild(familyId, "Maryam", UserType.Child),
                CreateChild(familyId, "Parent", UserType.Mother),
                CreateChild(familyId, "Father", UserType.Father),
                CreateChild(familyId, "Guardian", UserType.Guardian),
                CreateChild(familyId, "Other", (UserType)99)
            });

        var service = new UserChildrenService(Mock.Of<IConfiguration>(), repository.Object);

        var result = (await service.GetUserChilds(familyId)).ToList();

        Assert.Equal(4, result.Count);
        Assert.Contains(result, child => child.Result.UserType == UserType.Child);
        Assert.Contains(result, child => child.Result.UserType == UserType.Mother);
        Assert.Contains(result, child => child.Result.UserType == UserType.Father);
        Assert.Contains(result, child => child.Result.UserType == UserType.Guardian);
        Assert.DoesNotContain(result, child => (int)child.Result.UserType == 99);
    }

    [Fact]
    public async Task GetUserChilds_WhenFetchAdultsIsFalse_ReturnsSupportedFamilyMemberTypes()
    {
        var familyId = Guid.NewGuid();
        var repository = new Mock<IUserChildrenRepository>();
        repository
            .Setup(repo => repo.GetFamilyChildren(familyId))
            .ReturnsAsync(new[]
            {
                CreateChild(familyId, "Maryam", UserType.Child),
                CreateChild(familyId, "Parent", UserType.Father),
                CreateChild(familyId, "Guardian", UserType.Guardian),
                CreateChild(familyId, "Other", (UserType)99)
            });

        var service = new UserChildrenService(Mock.Of<IConfiguration>(), repository.Object);

        var result = (await service.GetUserChilds(familyId, fetchAdults: false)).ToList();

        Assert.Equal(3, result.Count);
        Assert.Contains(result, child => child.Result.UserType == UserType.Child);
        Assert.Contains(result, child => child.Result.UserType == UserType.Father);
        Assert.Contains(result, child => child.Result.UserType == UserType.Guardian);
        Assert.DoesNotContain(result, child => (int)child.Result.UserType == 99);
    }

    [Fact]
    public async Task GetUserChilds_WhenFetchAdultsIsTrue_ReturnsAllUserTypes()
    {
        var familyId = Guid.NewGuid();
        var repository = new Mock<IUserChildrenRepository>();
        repository
            .Setup(repo => repo.GetFamilyChildren(familyId))
            .ReturnsAsync(new[]
            {
                CreateChild(familyId, "Maryam", UserType.Child),
                CreateChild(familyId, "Parent", UserType.Mother),
                CreateChild(familyId, "Guardian", UserType.Guardian),
                CreateChild(familyId, "Other", (UserType)99)
            });

        var service = new UserChildrenService(Mock.Of<IConfiguration>(), repository.Object);

        var result = (await service.GetUserChilds(familyId, fetchAdults: true)).ToList();

        Assert.Equal(4, result.Count);
        Assert.Contains(result, child => child.Result.UserType == UserType.Child);
        Assert.Contains(result, child => child.Result.UserType == UserType.Mother);
        Assert.Contains(result, child => child.Result.UserType == UserType.Guardian);
        Assert.Contains(result, child => (int)child.Result.UserType == 99);
    }

    private static Child CreateChild(Guid familyId, string firstName, UserType userType)
    {
        return new Child
        {
            ChildId = Guid.NewGuid(),
            FamilyId = familyId,
            FirstName = firstName,
            LastName = "Test",
            UserType = userType,
            Gender = Gender.Unknown,
            AcedemicGroup = AcedemicGroupType.None,
            DateOfBirth = DateTime.UtcNow.AddYears(-10),
            RAMQExpiry = DateTime.UtcNow.AddYears(1),
            RAMQNumber = $"RAMQ-{firstName}",
            RAMQSequenceNumber = 1,
            HasAllergy = false,
            Allergies = string.Empty,
            OtherHealthConditions = string.Empty,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedOn = DateTime.UtcNow
        };
    }
}
