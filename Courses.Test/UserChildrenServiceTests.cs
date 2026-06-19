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
    public async Task GetUserChilds_DefaultsToChildUserTypeOnly()
    {
        var familyId = Guid.NewGuid();
        var repository = new Mock<IUserChildrenRepository>();
        repository
            .Setup(repo => repo.GetFamilyChildren(familyId))
            .ReturnsAsync(new[]
            {
                CreateChild(familyId, "Maryam", UserType.Child),
                CreateChild(familyId, "Parent", UserType.Mother),
                CreateChild(familyId, "Guardian", UserType.Guardian)
            });

        var service = new UserChildrenService(Mock.Of<IConfiguration>(), repository.Object);

        var result = (await service.GetUserChilds(familyId)).ToList();

        Assert.Single(result);
        Assert.Equal(UserType.Child, result[0].Result.UserType);
        Assert.Equal("Maryam", result[0].Result.FirstName);
    }

    [Fact]
    public async Task GetUserChilds_WhenFetchAdultsIsFalse_ReturnsOnlyChildren()
    {
        var familyId = Guid.NewGuid();
        var repository = new Mock<IUserChildrenRepository>();
        repository
            .Setup(repo => repo.GetFamilyChildren(familyId))
            .ReturnsAsync(new[]
            {
                CreateChild(familyId, "Maryam", UserType.Child),
                CreateChild(familyId, "Parent", UserType.Father)
            });

        var service = new UserChildrenService(Mock.Of<IConfiguration>(), repository.Object);

        var result = (await service.GetUserChilds(familyId, fetchAdults: false)).ToList();

        Assert.Single(result);
        Assert.Equal(UserType.Child, result[0].Result.UserType);
    }

    [Fact]
    public async Task GetUserChilds_WhenFetchAdultsIsTrue_ReturnsAdultsToo()
    {
        var familyId = Guid.NewGuid();
        var repository = new Mock<IUserChildrenRepository>();
        repository
            .Setup(repo => repo.GetFamilyChildren(familyId))
            .ReturnsAsync(new[]
            {
                CreateChild(familyId, "Maryam", UserType.Child),
                CreateChild(familyId, "Parent", UserType.Mother),
                CreateChild(familyId, "Guardian", UserType.Guardian)
            });

        var service = new UserChildrenService(Mock.Of<IConfiguration>(), repository.Object);

        var result = (await service.GetUserChilds(familyId, fetchAdults: true)).ToList();

        Assert.Equal(3, result.Count);
        Assert.Contains(result, child => child.Result.UserType == UserType.Child);
        Assert.Contains(result, child => child.Result.UserType == UserType.Mother);
        Assert.Contains(result, child => child.Result.UserType == UserType.Guardian);
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
