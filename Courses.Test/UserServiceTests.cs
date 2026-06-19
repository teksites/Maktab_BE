using Application.Users.Contracts;
using Application.Users.Implementation;
using Email;
using Microsoft.Extensions.Configuration;
using Moq;
using MaktabDataContracts.Enums;
using Users.Repository;
using Users.Services;

namespace Courses.Test;

public class UserServiceTests
{
    [Theory]
    [InlineData(Relationship.Mother, UserType.Mother)]
    [InlineData(Relationship.Father, UserType.Father)]
    [InlineData(Relationship.Guardian, UserType.Guardian)]
    public async Task VerifyUserVerificationCodes_CreatesLinkedChildForSupportedRelationships(Relationship relationship, UserType expectedUserType)
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var tempUser = new UserInformation
        {
            UserId = userId,
            FamilyId = familyId,
            FirstName = "Parent",
            LastName = "User",
            Email = "parent@example.com",
            Phone = "1234567890",
            UserName = "parent-user",
            Password = "hashed",
            IsActive = true,
            Relationship = relationship
        };

        Child? createdChild = null;
        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(repo => repo.AddUser(It.IsAny<UserInformation>()))
            .ReturnsAsync((UserInformation user) => user);

        var tempUserRepository = new Mock<ITempUserRepository>();
        tempUserRepository
            .Setup(repo => repo.VerifyTempUserVerificationCodes(It.IsAny<UserVerification>()))
            .ReturnsAsync(true);
        tempUserRepository
            .Setup(repo => repo.GetTempUserInformation(userId))
            .ReturnsAsync(tempUser);
        tempUserRepository
            .Setup(repo => repo.DeleteTempUser(userId))
            .ReturnsAsync(true);

        var childRepository = new Mock<IUserChildrenRepository>();
        childRepository
            .Setup(repo => repo.AddChild(It.IsAny<Child>()))
            .Callback<Child>(child => createdChild = child)
            .ReturnsAsync((Child child) => child);

        var service = CreateUserService(
            userRepository: userRepository,
            tempUserRepository: tempUserRepository,
            userChildrenRepository: childRepository);

        var result = await service.VerifyUserVerificationCodes(new UserVerification
        {
            UserId = userId,
            EmailVerificationCode = "email",
            PhoneVerificationCode = "phone"
        });

        Assert.True(result);
        Assert.NotNull(createdChild);
        Assert.Equal(userId, createdChild!.ChildId);
        Assert.Equal(familyId, createdChild.FamilyId);
        Assert.Equal(tempUser.FirstName, createdChild.FirstName);
        Assert.Equal(tempUser.LastName, createdChild.LastName);
        Assert.Equal(expectedUserType, createdChild.UserType);
        Assert.Equal(Gender.Unknown, createdChild.Gender);
        Assert.Equal(AcedemicGroupType.None, createdChild.AcedemicGroup);
        Assert.Equal(string.Empty, createdChild.RAMQNumber);
        Assert.Equal(0, createdChild.RAMQSequenceNumber);
    }

    [Fact]
    public async Task VerifyUserVerificationCodes_DoesNotCreateLinkedChildForOtherRelationships()
    {
        var userId = Guid.NewGuid();
        var tempUser = new UserInformation
        {
            UserId = userId,
            FamilyId = Guid.NewGuid(),
            FirstName = "Teacher",
            LastName = "User",
            Email = "teacher@example.com",
            Phone = "1234567890",
            UserName = "teacher-user",
            Password = "hashed",
            IsActive = true,
            Relationship = Relationship.Teacher
        };

        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(repo => repo.AddUser(It.IsAny<UserInformation>()))
            .ReturnsAsync((UserInformation user) => user);

        var tempUserRepository = new Mock<ITempUserRepository>();
        tempUserRepository
            .Setup(repo => repo.VerifyTempUserVerificationCodes(It.IsAny<UserVerification>()))
            .ReturnsAsync(true);
        tempUserRepository
            .Setup(repo => repo.GetTempUserInformation(userId))
            .ReturnsAsync(tempUser);
        tempUserRepository
            .Setup(repo => repo.DeleteTempUser(userId))
            .ReturnsAsync(true);

        var childRepository = new Mock<IUserChildrenRepository>();

        var service = CreateUserService(
            userRepository: userRepository,
            tempUserRepository: tempUserRepository,
            userChildrenRepository: childRepository);

        var result = await service.VerifyUserVerificationCodes(new UserVerification
        {
            UserId = userId,
            EmailVerificationCode = "email",
            PhoneVerificationCode = "phone"
        });

        Assert.True(result);
        childRepository.Verify(repo => repo.AddChild(It.IsAny<Child>()), Times.Never);
    }

    [Fact]
    public async Task VerifyUserVerificationCodes_DeletesUserWhenLinkedChildCreationFails()
    {
        var userId = Guid.NewGuid();
        var tempUser = new UserInformation
        {
            UserId = userId,
            FamilyId = Guid.NewGuid(),
            FirstName = "Guardian",
            LastName = "User",
            Email = "guardian@example.com",
            Phone = "1234567890",
            UserName = "guardian-user",
            Password = "hashed",
            IsActive = true,
            Relationship = Relationship.Guardian
        };

        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(repo => repo.AddUser(It.IsAny<UserInformation>()))
            .ReturnsAsync((UserInformation user) => user);
        userRepository
            .Setup(repo => repo.DeleteUser(userId, true))
            .ReturnsAsync(true);

        var tempUserRepository = new Mock<ITempUserRepository>();
        tempUserRepository
            .Setup(repo => repo.VerifyTempUserVerificationCodes(It.IsAny<UserVerification>()))
            .ReturnsAsync(true);
        tempUserRepository
            .Setup(repo => repo.GetTempUserInformation(userId))
            .ReturnsAsync(tempUser);

        var childRepository = new Mock<IUserChildrenRepository>();
        childRepository
            .Setup(repo => repo.AddChild(It.IsAny<Child>()))
            .ThrowsAsync(new InvalidOperationException("insert failed"));

        var service = CreateUserService(
            userRepository: userRepository,
            tempUserRepository: tempUserRepository,
            userChildrenRepository: childRepository);

        var result = await service.VerifyUserVerificationCodes(new UserVerification
        {
            UserId = userId,
            EmailVerificationCode = "email",
            PhoneVerificationCode = "phone"
        });

        Assert.False(result);
        userRepository.Verify(repo => repo.DeleteUser(userId, true), Times.Once);
        tempUserRepository.Verify(repo => repo.DeleteTempUser(It.IsAny<Guid>()), Times.Never);
    }

    private static UserService CreateUserService(
        Mock<IUserRepository>? userRepository = null,
        Mock<ITempUserRepository>? tempUserRepository = null,
        Mock<IUserChildrenRepository>? userChildrenRepository = null)
    {
        return new UserService(
            Mock.Of<IConfiguration>(),
            (userRepository ?? new Mock<IUserRepository>()).Object,
            (tempUserRepository ?? new Mock<ITempUserRepository>()).Object,
            new Mock<IAddressService>().Object,
            new Mock<IOtherContactsService>().Object,
            new Mock<IUserChildrenService>().Object,
            (userChildrenRepository ?? new Mock<IUserChildrenRepository>()).Object,
            new Mock<ISendEmailService>().Object);
    }
}
