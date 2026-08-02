using Application.Users.Contracts;
using Application.Users.Implementation;
using Email;
using Microsoft.Extensions.Configuration;
using Moq;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Responses.Addresses;
using MaktabDataContracts.Responses.OtherContacts;
using MaktabDataContracts.Requests.Users;
using Users.Contracts;
using Users.Repository;
using Users.Services;
using Users.Utils.Implementation;

namespace Courses.Test;

public class UserServiceTests
{
    [Theory]
    [InlineData(Relationship.Mother, UserType.Mother, Gender.Female)]
    [InlineData(Relationship.Father, UserType.Father, Gender.Male)]
    [InlineData(Relationship.Guardian, UserType.Guardian, Gender.Unknown)]
    public async Task VerifyUserVerificationCodes_CreatesLinkedChildForSupportedRelationships(
        Relationship relationship,
        UserType expectedUserType,
        Gender expectedGender)
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
        Assert.Equal(expectedGender, createdChild.Gender);
        Assert.Equal(AcedemicGroupType.Adults, createdChild.AcedemicGroup);
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
    public async Task AddTemporaryUser_RejectsDuplicateMotherForFamily()
    {
        var familyId = Guid.NewGuid();
        var addUser = new AddUserInformation
        {
            FamilyId = familyId,
            UserName = "duplicate-mother",
            Email = "duplicate@example.com",
            Phone = "1234567890",
            FirstName = "Duplicate",
            LastName = "Mother",
            Password = "Password123",
            Relationship = Relationship.Mother,
            UserRoles = new List<string> { UserRoleType.Normal.ToString() }
        };

        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(repo => repo.GetAllFamilyUsersInformation(familyId, true))
            .ReturnsAsync(new[]
            {
                new UserInformation
                {
                    UserId = Guid.NewGuid(),
                    FamilyId = familyId,
                    FirstName = "Existing",
                    LastName = "Mother",
                    Relationship = Relationship.Mother,
                    IfTempUser = false,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                    UpdatedOn = DateTime.UtcNow.AddDays(-1)
                }
            });

        var tempUserRepository = new Mock<ITempUserRepository>();
        var service = CreateUserService(
            userRepository: userRepository,
            tempUserRepository: tempUserRepository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddTemporaryUser(addUser));

        Assert.Equal("Mother is already added and multiple same parents can't be added", exception.Message);
        tempUserRepository.Verify(repo => repo.AddTemporaryUser(It.IsAny<UserRegistrationInformation>()), Times.Never);
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

    [Fact]
    public async Task VerifyUserVerificationCodes_RejectsDuplicateFatherBeforeCreatingUser()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var tempUser = new UserInformation
        {
            UserId = userId,
            FamilyId = familyId,
            FirstName = "Pending",
            LastName = "Father",
            Email = "pending@example.com",
            Phone = "1234567890",
            UserName = "pending-father",
            Password = "hashed",
            IsActive = true,
            Relationship = Relationship.Father
        };

        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(repo => repo.GetAllFamilyUsersInformation(familyId, true))
            .ReturnsAsync(new[]
            {
                tempUser,
                new UserInformation
                {
                    UserId = Guid.NewGuid(),
                    FamilyId = familyId,
                    FirstName = "Existing",
                    LastName = "Father",
                    Relationship = Relationship.Father,
                    IfTempUser = false,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-3),
                    UpdatedOn = DateTime.UtcNow.AddDays(-2)
                }
            });

        var tempUserRepository = new Mock<ITempUserRepository>();
        tempUserRepository
            .Setup(repo => repo.VerifyTempUserVerificationCodes(It.IsAny<UserVerification>()))
            .ReturnsAsync(true);
        tempUserRepository
            .Setup(repo => repo.GetTempUserInformation(userId))
            .ReturnsAsync(tempUser);

        var service = CreateUserService(
            userRepository: userRepository,
            tempUserRepository: tempUserRepository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.VerifyUserVerificationCodes(new UserVerification
        {
            UserId = userId,
            EmailVerificationCode = "email",
            PhoneVerificationCode = "phone"
        }));

        Assert.Equal("Father is already added and multiple same parents can't be added", exception.Message);
        userRepository.Verify(repo => repo.AddUser(It.IsAny<UserInformation>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPassword_SetsTemporaryPasswordFlagAndSendsEmail()
    {
        var userId = Guid.NewGuid();
        var user = new UserInformation
        {
            UserId = userId,
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            UserName = "test-user",
            Phone = "1234567890",
            IsActive = true
        };

        UpdateUserPassword? capturedPasswordUpdate = null;
        bool? capturedTempPasswordFlag = null;
        EmailData? capturedEmail = null;

        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(repo => repo.GetUserInformation(user.UserName, null, true))
            .ReturnsAsync(user);
        userRepository
            .Setup(repo => repo.UpdateUser(It.IsAny<UpdateUserPassword>(), It.IsAny<bool>()))
            .Callback<UpdateUserPassword, bool>((request, flag) =>
            {
                capturedPasswordUpdate = request;
                capturedTempPasswordFlag = flag;
            })
            .ReturnsAsync(user);

        var sendEmailService = new Mock<ISendEmailService>();
        sendEmailService
            .Setup(service => service.SendEmail(It.IsAny<EmailData>()))
            .Callback<EmailData>(email => capturedEmail = email)
            .ReturnsAsync(true);

        var service = CreateUserService(
            userRepository: userRepository,
            sendEmailService: sendEmailService);

        var result = await service.ForgotPassword(user.UserName, null);

        Assert.True(result);
        Assert.NotNull(capturedPasswordUpdate);
        Assert.Equal(userId, capturedPasswordUpdate!.UserId);
        Assert.Equal(string.Empty, capturedPasswordUpdate.OldPassword);
        Assert.False(string.IsNullOrWhiteSpace(capturedPasswordUpdate.NewPassword));
        Assert.True(capturedTempPasswordFlag);
        Assert.NotNull(capturedEmail);
        Assert.Equal(user.Email, capturedEmail!.To);
        Assert.Contains(capturedPasswordUpdate.NewPassword, capturedEmail.Body);
    }

    [Fact]
    public async Task ResetUserPassword_WithMatchingOldPassword_ClearsTemporaryPasswordFlag()
    {
        var userId = Guid.NewGuid();
        const string tempPassword = "TempPassword123";
        const string newPassword = "NewPassword456";

        UpdateUserPassword? capturedPasswordUpdate = null;
        bool? capturedTempPasswordFlag = null;

        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(repo => repo.GetUserInformation(userId))
            .ReturnsAsync(new UserInformation
            {
                UserId = userId,
                Password = PasswordHelper.HashPassword(tempPassword),
                IsTempPassword = true,
                IsActive = true
            });
        userRepository
            .Setup(repo => repo.UpdateUser(It.IsAny<UpdateUserPassword>(), It.IsAny<bool>()))
            .Callback<UpdateUserPassword, bool>((request, flag) =>
            {
                capturedPasswordUpdate = request;
                capturedTempPasswordFlag = flag;
            })
            .ReturnsAsync(new UserInformation
            {
                UserId = userId,
                Password = PasswordHelper.HashPassword(newPassword),
                IsTempPassword = false,
                IsActive = true
            });

        var service = CreateUserService(userRepository: userRepository);

        var result = await service.ResetUserPassword(new UpdateUserPassword
        {
            UserId = userId,
            OldPassword = tempPassword,
            NewPassword = newPassword
        });

        Assert.True(result);
        Assert.NotNull(capturedPasswordUpdate);
        Assert.Equal(userId, capturedPasswordUpdate!.UserId);
        Assert.Equal(tempPassword, capturedPasswordUpdate.OldPassword);
        Assert.Equal(newPassword, capturedPasswordUpdate.NewPassword);
        Assert.False(capturedTempPasswordFlag);
    }

    [Fact]
    public async Task ResetUserPassword_WithWrongOldPassword_DoesNotUpdatePassword()
    {
        var userId = Guid.NewGuid();

        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(repo => repo.GetUserInformation(userId))
            .ReturnsAsync(new UserInformation
            {
                UserId = userId,
                Password = PasswordHelper.HashPassword("ExpectedTempPassword"),
                IsTempPassword = true,
                IsActive = true
            });

        var service = CreateUserService(userRepository: userRepository);

        var result = await service.ResetUserPassword(new UpdateUserPassword
        {
            UserId = userId,
            OldPassword = "WrongPassword",
            NewPassword = "NewPassword456"
        });

        Assert.False(result);
        userRepository.Verify(repo => repo.UpdateUser(It.IsAny<UpdateUserPassword>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task AddTemporaryUser_SendsBilingualActivationEmail()
    {
        var addUser = new AddUserInformation
        {
            UserName = "test-user",
            Email = "test@example.com",
            Phone = "1234567890",
            FirstName = "Test",
            LastName = "User",
            Password = "Password123",
            Relationship = Relationship.Mother,
            UserRoles = new List<string> { UserRoleType.Normal.ToString() }
        };

        EmailData? capturedEmail = null;
        var tempUserRepository = new Mock<ITempUserRepository>();
        tempUserRepository
            .Setup(repo => repo.AddTemporaryUser(It.IsAny<UserRegistrationInformation>()))
            .ReturnsAsync((UserRegistrationInformation request) => new UserInformation
            {
                UserId = request.UserId,
                FamilyId = request.FamilyId,
                UserName = request.UserName,
                Email = request.Email,
                Phone = request.Phone,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Password = request.Password,
                IsActive = true,
                Relationship = request.Relationship,
                UserRole = request.UserRole
            });

        var sendEmailService = new Mock<ISendEmailService>();
        sendEmailService
            .Setup(service => service.SendEmail(It.IsAny<EmailData>()))
            .Callback<EmailData>(email => capturedEmail = email)
            .ReturnsAsync(true);

        var service = CreateUserService(
            tempUserRepository: tempUserRepository,
            sendEmailService: sendEmailService);

        var result = await service.AddTemporaryUser(addUser);

        Assert.NotNull(result);
        Assert.NotNull(capturedEmail);
        Assert.Equal(addUser.Email, capturedEmail!.To);
        Assert.Equal("ICC Maktab account registration activation code - code d’activation d’inscription ICC Maktab", capturedEmail.Subject);
        Assert.Contains("Greetings Test User", capturedEmail.Body);
        Assert.Contains("Activation code for the registration of your ICC Brossard Schools and Activities portal account (Maktab).", capturedEmail.Body);
        Assert.Contains("Bonjour Test User", capturedEmail.Body);
        Assert.Contains("Code d'activation pour l'inscription a votre compte sur le portail des ecoles et activites ICC Brossard (Maktab).", capturedEmail.Body);
    }

    [Fact]
    public async Task SendActivationCode_SendsBilingualActivationEmail()
    {
        var userId = Guid.NewGuid();
        EmailData? capturedEmail = null;

        var tempUserRepository = new Mock<ITempUserRepository>();
        tempUserRepository
            .Setup(repo => repo.UpdateRegistrationActivationCodes(It.IsAny<UpdateUserRegistrationInformation>()))
            .ReturnsAsync((UpdateUserRegistrationInformation request) => new UserInformation
            {
                UserId = request.UserId,
                FirstName = "Test",
                LastName = "User",
                Email = "test@example.com",
                UserName = "test-user",
                Phone = "1234567890",
                IsActive = true
            });

        var sendEmailService = new Mock<ISendEmailService>();
        sendEmailService
            .Setup(service => service.SendEmail(It.IsAny<EmailData>()))
            .Callback<EmailData>(email => capturedEmail = email)
            .ReturnsAsync(true);

        var service = CreateUserService(
            tempUserRepository: tempUserRepository,
            sendEmailService: sendEmailService);

        var result = await service.SendActivationCode(userId);

        Assert.True(result);
        Assert.NotNull(capturedEmail);
        Assert.Equal("test@example.com", capturedEmail!.To);
        Assert.Equal("ICC Maktab account registration activation code - code d’activation d’inscription ICC Maktab", capturedEmail.Subject);
        Assert.Contains("Greetings Test User", capturedEmail.Body);
        Assert.Contains("Bonjour Test User", capturedEmail.Body);
        Assert.Contains("Veuillez saisir ce code pour activer votre compte sur le portail des ecoles et activites ICC Brossard.", capturedEmail.Body);
    }

    [Fact]
    public async Task GetAllFamilyUsersInformation_PrefersLatestVerifiedParentThenLatestPendingParent()
    {
        var familyId = Guid.NewGuid();
        var latestVerifiedMotherId = Guid.NewGuid();
        var latestPendingFatherId = Guid.NewGuid();

        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(repo => repo.GetAllFamilyUsersInformation(familyId, true))
            .ReturnsAsync(new[]
            {
                new UserInformation
                {
                    UserId = Guid.NewGuid(),
                    FamilyId = familyId,
                    FirstName = "Older",
                    LastName = "Mother",
                    Email = "older-mother@example.com",
                    Relationship = Relationship.Mother,
                    IfTempUser = false,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-5),
                    UpdatedOn = DateTime.UtcNow.AddDays(-4)
                },
                new UserInformation
                {
                    UserId = latestVerifiedMotherId,
                    FamilyId = familyId,
                    FirstName = "Latest",
                    LastName = "Mother",
                    Email = "latest-mother@example.com",
                    Relationship = Relationship.Mother,
                    IfTempUser = false,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-3),
                    UpdatedOn = DateTime.UtcNow.AddDays(-1)
                },
                new UserInformation
                {
                    UserId = Guid.NewGuid(),
                    FamilyId = familyId,
                    FirstName = "Older",
                    LastName = "Father",
                    Email = "older-father@example.com",
                    Relationship = Relationship.Father,
                    IfTempUser = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-6),
                    UpdatedOn = DateTime.UtcNow.AddDays(-5)
                },
                new UserInformation
                {
                    UserId = latestPendingFatherId,
                    FamilyId = familyId,
                    FirstName = "Latest",
                    LastName = "Father",
                    Email = "latest-father@example.com",
                    Relationship = Relationship.Father,
                    IfTempUser = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                    UpdatedOn = DateTime.UtcNow
                },
                new UserInformation
                {
                    UserId = Guid.NewGuid(),
                    FamilyId = familyId,
                    FirstName = "Guardian",
                    LastName = "Person",
                    Email = "guardian@example.com",
                    Relationship = Relationship.Guardian,
                    IfTempUser = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    UpdatedOn = DateTime.UtcNow.AddHours(-1)
                }
            });

        var service = CreateUserService(userRepository: userRepository);

        var result = (await service.GetAllFamilyUsersInformation(familyId)).ToList();

        Assert.Equal(3, result.Count);
        Assert.Contains(result, user => user.UserId == latestVerifiedMotherId && !user.IfTempUser);
        Assert.Contains(result, user => user.UserId == latestPendingFatherId && user.IfTempUser);
        Assert.Contains(result, user => user.Relationship == Relationship.Guardian);
        Assert.Equal(1, result.Count(user => user.Relationship == Relationship.Mother));
        Assert.Equal(1, result.Count(user => user.Relationship == Relationship.Father));
    }

    [Fact]
    public async Task GetFamilyInformation_ReturnsParentsGuardiansOtherContactsAndAddresses()
    {
        var familyId = Guid.NewGuid();
        var latestMotherId = Guid.NewGuid();
        var latestFatherId = Guid.NewGuid();
        var guardianId = Guid.NewGuid();
        var otherContactId = Guid.NewGuid();
        var addressId = Guid.NewGuid();

        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(repo => repo.GetAllFamilyUsersInformation(familyId, true))
            .ReturnsAsync(new[]
            {
                new UserInformation
                {
                    UserId = Guid.NewGuid(),
                    FamilyId = familyId,
                    FirstName = "Older",
                    LastName = "Mother",
                    Email = "older-mother@example.com",
                    Phone = "1111111111",
                    Relationship = Relationship.Mother,
                    IfTempUser = false,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-4),
                    UpdatedOn = DateTime.UtcNow.AddDays(-3)
                },
                new UserInformation
                {
                    UserId = latestMotherId,
                    FamilyId = familyId,
                    FirstName = "Latest",
                    LastName = "Mother",
                    Email = "latest-mother@example.com",
                    Phone = "2222222222",
                    Relationship = Relationship.Mother,
                    IfTempUser = false,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                    UpdatedOn = DateTime.UtcNow.AddDays(-1)
                },
                new UserInformation
                {
                    UserId = latestFatherId,
                    FamilyId = familyId,
                    FirstName = "Pending",
                    LastName = "Father",
                    Email = "pending-father@example.com",
                    Phone = "3333333333",
                    Relationship = Relationship.Father,
                    IfTempUser = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    UpdatedOn = DateTime.UtcNow
                },
                new UserInformation
                {
                    UserId = guardianId,
                    FamilyId = familyId,
                    FirstName = "Primary",
                    LastName = "Guardian",
                    Email = "guardian@example.com",
                    Phone = "4444444444",
                    Relationship = Relationship.Guardian,
                    IfTempUser = false,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-3),
                    UpdatedOn = DateTime.UtcNow.AddHours(-12)
                },
                new UserInformation
                {
                    UserId = Guid.NewGuid(),
                    FamilyId = familyId,
                    FirstName = "Not",
                    LastName = "Included",
                    Email = "teacher@example.com",
                    Phone = "5555555555",
                    Relationship = Relationship.Teacher,
                    IfTempUser = false,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-5),
                    UpdatedOn = DateTime.UtcNow.AddDays(-4)
                }
            });

        var otherContactsService = new Mock<IOtherContactsService>();
        otherContactsService
            .Setup(service => service.GetFamilyOtherContacts(familyId, It.IsAny<IEnumerable<ContactType>>()))
            .ReturnsAsync(new[]
            {
                new OtherContactResponse
                {
                    ContactId = otherContactId,
                    FamilyId = familyId,
                    FirstName = "Support",
                    LastName = "Contact",
                    Phone = "6666666666",
                    Relationship = Relationship.Aunt,
                    ContactType = ContactType.Emergency,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    UpdatedOn = DateTime.UtcNow
                }
            });

        var addressService = new Mock<IAddressService>();
        addressService
            .Setup(service => service.GetAddressWithConnectedId(familyId, false))
            .ReturnsAsync(new[]
            {
                new AddressResponse
                {
                    AddressId = addressId,
                    ConnectedId = familyId,
                    AddressLine1 = "123 Main St",
                    City = "Montreal",
                    Province = "QC",
                    Country = "Canada",
                    PostalCode = "H1H1H1",
                    HomeAddress = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-10),
                    UpdatedOn = DateTime.UtcNow.AddDays(-1)
                }
            });

        var service = CreateUserService(
            userRepository: userRepository,
            addressService: addressService,
            otherContactsService: otherContactsService);

        var result = await service.GetFamilyInformation(familyId);

        Assert.Equal(3, result.FamilyInformation.Count);
        Assert.Contains(result.FamilyInformation, member => member.UserId == latestMotherId && member.UserName == "Latest Mother");
        Assert.Contains(result.FamilyInformation, member => member.UserId == latestFatherId && member.UserName == "Pending Father");
        Assert.Contains(result.FamilyInformation, member => member.UserId == guardianId && member.UserName == "Primary Guardian");
        Assert.DoesNotContain(result.FamilyInformation, member => member.Relationship == Relationship.Teacher);

        Assert.Single(result.OtherContacts);
        Assert.Equal(otherContactId, result.OtherContacts[0].ContactId);
        Assert.Equal("Support Contact", result.OtherContacts[0].UserName);
        Assert.Equal("6666666666", result.OtherContacts[0].Phone);
        Assert.Equal(Relationship.Aunt, result.OtherContacts[0].Relationship);
        Assert.Equal(ContactType.Emergency, result.OtherContacts[0].ContactType);

        Assert.Single(result.FamilyAddress);
        Assert.Equal(addressId, result.FamilyAddress[0].AddressId);
        Assert.Equal(familyId, result.FamilyAddress[0].ConnectedId);
    }

    private static UserService CreateUserService(
        Mock<IUserRepository>? userRepository = null,
        Mock<ITempUserRepository>? tempUserRepository = null,
        Mock<IUserChildrenRepository>? userChildrenRepository = null,
        Mock<ISendEmailService>? sendEmailService = null,
        Mock<IAddressService>? addressService = null,
        Mock<IOtherContactsService>? otherContactsService = null)
    {
        return new UserService(
            Mock.Of<IConfiguration>(),
            (userRepository ?? new Mock<IUserRepository>()).Object,
            (tempUserRepository ?? new Mock<ITempUserRepository>()).Object,
            (addressService ?? new Mock<IAddressService>()).Object,
            (otherContactsService ?? new Mock<IOtherContactsService>()).Object,
            new Mock<IUserChildrenService>().Object,
            (userChildrenRepository ?? new Mock<IUserChildrenRepository>()).Object,
            (sendEmailService ?? new Mock<ISendEmailService>()).Object);
    }
}
