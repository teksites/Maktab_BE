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
}
