using Application.Users.Contracts;
using Email;
using Microsoft.Extensions.Configuration;
using Moq;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Authentication;
using Users.Contracts;
using Users.Implementation.Services;
using Users.Repository;
using Users.Services;
using AddSession = InternalContracts.AddSession;
using AddSessionTwoFactorCode = InternalContracts.AddSessionTwoFactorCode;
using SessionTwoFactorCode = InternalContracts.SessionTwoFactorCode;

namespace Courses.Test;

public class UserLoginServiceTests
{
    [Fact]
    public async Task Authenticate_WhenMfaDisabled_PreservesExistingLoginFlow()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        AddSession? capturedSession = null;

        var repository = new Mock<IUserLoginRepository>();
        repository
            .Setup(repo => repo.GetSessionByUserId(userId))
            .ReturnsAsync(Guid.Empty);
        repository
            .Setup(repo => repo.LogInSession(It.IsAny<AddSession>()))
            .Callback<AddSession>(session => capturedSession = session)
            .ReturnsAsync(true);

        var userService = new Mock<IUserService>();
        userService
            .Setup(service => service.GetUserInformation("verified-user", "Password123", false))
            .ReturnsAsync(new MaktabDataContracts.Responses.Users.UserInformationResponse
            {
                UserId = userId,
                FamilyId = familyId,
                Email = "verified@example.com",
                FirstName = "Verified",
                LastName = "User",
                IfTempUser = false,
                IsMultiFactorLoginEnabled = false
            });

        var sendEmailService = new Mock<ISendEmailService>();
        var service = CreateService(repository, userService, sendEmailService);

        var response = await service.Authenticate("verified-user", "Password123", "127.0.0.1");

        Assert.NotNull(response);
        Assert.False(response.RequiresTwoFactorVerification);
        Assert.True(response.IsTwoFactorVerified);
        Assert.Null(response.TwoFactorCodeExpiresOn);

        Assert.NotNull(capturedSession);
        Assert.False(capturedSession!.RequiresTwoFactorVerification);
        Assert.True(capturedSession.IsTwoFactorVerified);
        Assert.NotNull(capturedSession.TwoFactorVerifiedOn);

        repository.Verify(repo => repo.AddSessionTwoFactorCode(It.IsAny<AddSessionTwoFactorCode>()), Times.Never);
        sendEmailService.Verify(service => service.SendEmail(It.IsAny<EmailData>()), Times.Never);
    }

    [Fact]
    public async Task Authenticate_WhenMfaEnabled_CreatesPendingSessionAndSendsCode()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        AddSession? capturedSession = null;
        AddSessionTwoFactorCode? capturedCode = null;
        EmailData? capturedEmail = null;

        var repository = new Mock<IUserLoginRepository>();
        repository
            .Setup(repo => repo.GetSessionByUserId(userId))
            .ReturnsAsync(Guid.Empty);
        repository
            .Setup(repo => repo.LogInSession(It.IsAny<AddSession>()))
            .Callback<AddSession>(session => capturedSession = session)
            .ReturnsAsync(true);
        repository
            .Setup(repo => repo.DeactivateSessionTwoFactorCodes(It.IsAny<Guid>()))
            .ReturnsAsync(true);
        repository
            .Setup(repo => repo.AddSessionTwoFactorCode(It.IsAny<AddSessionTwoFactorCode>()))
            .Callback<AddSessionTwoFactorCode>(code => capturedCode = code)
            .ReturnsAsync(true);

        var userService = new Mock<IUserService>();
        userService
            .Setup(service => service.GetUserInformation("mfa-user", "Password123", false))
            .ReturnsAsync(new MaktabDataContracts.Responses.Users.UserInformationResponse
            {
                UserId = userId,
                FamilyId = familyId,
                Email = "mfa@example.com",
                FirstName = "Mfa",
                LastName = "User",
                IfTempUser = false,
                IsMultiFactorLoginEnabled = true
            });
        userService
            .Setup(service => service.GetUserRoles(userId))
            .ReturnsAsync(UserRoleType.Normal);

        var sendEmailService = new Mock<ISendEmailService>();
        sendEmailService
            .Setup(service => service.SendEmail(It.IsAny<EmailData>()))
            .Callback<EmailData>(email => capturedEmail = email)
            .ReturnsAsync(true);

        var service = CreateService(repository, userService, sendEmailService);

        var beforeAuthentication = DateTime.UtcNow;
        var response = await service.Authenticate("mfa-user", "Password123", "127.0.0.1");

        Assert.NotNull(response);
        Assert.True(response.RequiresTwoFactorVerification);
        Assert.False(response.IsTwoFactorVerified);
        Assert.NotNull(response.TwoFactorCodeExpiresOn);

        Assert.NotNull(capturedSession);
        Assert.True(capturedSession!.RequiresTwoFactorVerification);
        Assert.False(capturedSession.IsTwoFactorVerified);
        Assert.Null(capturedSession.TwoFactorVerifiedOn);

        Assert.NotNull(capturedCode);
        Assert.Equal(userId, capturedCode!.UserId);
        Assert.Equal("mfa@example.com", capturedCode.Email);
        Assert.True(capturedCode.IsActive);
        Assert.False(capturedCode.IsVerified);
        Assert.InRange(capturedCode.ExpiresOn, beforeAuthentication.AddMinutes(30).AddSeconds(-1), DateTime.UtcNow.AddMinutes(30).AddSeconds(1));

        Assert.NotNull(capturedEmail);
        Assert.Equal("mfa@example.com", capturedEmail!.To);
        Assert.Contains("verification code", capturedEmail.Subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Authenticate_WhenTempUser_DoesNotRequireMfa()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        AddSession? capturedSession = null;

        var repository = new Mock<IUserLoginRepository>();
        repository
            .Setup(repo => repo.GetSessionByUserId(userId))
            .ReturnsAsync(Guid.Empty);
        repository
            .Setup(repo => repo.LogInSession(It.IsAny<AddSession>()))
            .Callback<AddSession>(session => capturedSession = session)
            .ReturnsAsync(true);

        var userService = new Mock<IUserService>();
        userService
            .Setup(service => service.GetUserInformation("pending-user", "Password123", false))
            .ReturnsAsync(new MaktabDataContracts.Responses.Users.UserInformationResponse
            {
                UserId = userId,
                FamilyId = familyId,
                Email = "pending@example.com",
                FirstName = "Pending",
                LastName = "User",
                IfTempUser = true,
                IsMultiFactorLoginEnabled = true
            });

        var sendEmailService = new Mock<ISendEmailService>();
        var service = CreateService(repository, userService, sendEmailService);

        var response = await service.Authenticate("pending-user", "Password123", "127.0.0.1");

        Assert.NotNull(response);
        Assert.False(response.RequiresTwoFactorVerification);
        Assert.True(response.IsTwoFactorVerified);
        Assert.Null(response.TwoFactorCodeExpiresOn);

        Assert.NotNull(capturedSession);
        Assert.False(capturedSession!.RequiresTwoFactorVerification);
        Assert.True(capturedSession.IsTwoFactorVerified);

        repository.Verify(repo => repo.AddSessionTwoFactorCode(It.IsAny<AddSessionTwoFactorCode>()), Times.Never);
        sendEmailService.Verify(service => service.SendEmail(It.IsAny<EmailData>()), Times.Never);
    }

    [Fact]
    public async Task VerifyTwoFactorLogin_WhenCodeMatches_VerifiesSession()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var code = "ABC123";
        var verifiedOnBefore = DateTime.UtcNow;

        var repository = new Mock<IUserLoginRepository>();
        repository
            .Setup(repo => repo.GetSessionAuthenticationState(sessionId))
            .ReturnsAsync(new SessionAuthenticationState
            {
                SessionId = sessionId,
                UserId = userId,
                FamilyId = Guid.NewGuid(),
                IsActive = true,
                RequiresTwoFactorVerification = true,
                IsTwoFactorVerified = false
            });
        repository
            .Setup(repo => repo.GetActiveSessionTwoFactorCode(sessionId))
            .ReturnsAsync(new SessionTwoFactorCode
            {
                SessionTwoFactorCodeId = Guid.NewGuid(),
                SessionId = sessionId,
                UserId = userId,
                Email = "mfa@example.com",
                VerificationCodeHash = Hash(code),
                ExpiresOn = DateTime.UtcNow.AddMinutes(10),
                AttemptCount = 0,
                IsActive = true,
                IsVerified = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedOn = DateTime.UtcNow
            });
        repository
            .Setup(repo => repo.MarkSessionTwoFactorCodeVerified(It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .ReturnsAsync(true);
        repository
            .Setup(repo => repo.MarkSessionTwoFactorVerified(sessionId, It.IsAny<DateTime>()))
            .ReturnsAsync(true);

        var userService = new Mock<IUserService>();
        userService
            .Setup(service => service.GetUserInformation("mfa-user", null, true))
            .ReturnsAsync(new MaktabDataContracts.Responses.Users.UserInformationResponse
            {
                UserId = userId,
                FamilyId = Guid.NewGuid(),
                Email = "mfa@example.com",
                FirstName = "Mfa",
                LastName = "User",
                IfTempUser = false,
                IsMultiFactorLoginEnabled = true
            });

        var service = CreateService(repository, userService, new Mock<ISendEmailService>());

        var response = await service.VerifyTwoFactorLogin(sessionId, "mfa-user", new VerifyTwoFactorLoginRequest
        {
            VerificationCode = code
        });

        Assert.True(response.Success);
        Assert.True(response.IsTwoFactorVerified);
        Assert.NotNull(response.TwoFactorVerifiedOn);
        Assert.True(response.TwoFactorVerifiedOn >= verifiedOnBefore);

        repository.Verify(repo => repo.MarkSessionTwoFactorCodeVerified(It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Once);
        repository.Verify(repo => repo.MarkSessionTwoFactorVerified(sessionId, It.IsAny<DateTime>()), Times.Once);
        repository.Verify(repo => repo.IncrementSessionTwoFactorAttemptCount(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task VerifyTwoFactorLogin_WhenCodeInvalid_IncrementsAttemptsAndFails()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var repository = new Mock<IUserLoginRepository>();
        repository
            .Setup(repo => repo.GetSessionAuthenticationState(sessionId))
            .ReturnsAsync(new SessionAuthenticationState
            {
                SessionId = sessionId,
                UserId = userId,
                FamilyId = Guid.NewGuid(),
                IsActive = true,
                RequiresTwoFactorVerification = true,
                IsTwoFactorVerified = false
            });
        repository
            .Setup(repo => repo.GetActiveSessionTwoFactorCode(sessionId))
            .ReturnsAsync(new SessionTwoFactorCode
            {
                SessionTwoFactorCodeId = Guid.NewGuid(),
                SessionId = sessionId,
                UserId = userId,
                Email = "mfa@example.com",
                VerificationCodeHash = Hash("ABC123"),
                ExpiresOn = DateTime.UtcNow.AddMinutes(10),
                AttemptCount = 0,
                IsActive = true,
                IsVerified = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedOn = DateTime.UtcNow
            });
        repository
            .Setup(repo => repo.IncrementSessionTwoFactorAttemptCount(It.IsAny<Guid>()))
            .ReturnsAsync(true);

        var userService = new Mock<IUserService>();
        userService
            .Setup(service => service.GetUserInformation("mfa-user", null, true))
            .ReturnsAsync(new MaktabDataContracts.Responses.Users.UserInformationResponse
            {
                UserId = userId,
                FamilyId = Guid.NewGuid(),
                Email = "mfa@example.com",
                FirstName = "Mfa",
                LastName = "User",
                IfTempUser = false,
                IsMultiFactorLoginEnabled = true
            });

        var service = CreateService(repository, userService, new Mock<ISendEmailService>());

        var response = await service.VerifyTwoFactorLogin(sessionId, "mfa-user", new VerifyTwoFactorLoginRequest
        {
            VerificationCode = "WRONG1"
        });

        Assert.False(response.Success);
        Assert.False(response.IsTwoFactorVerified);
        Assert.Equal("Invalid verification code.", response.Message);

        repository.Verify(repo => repo.IncrementSessionTwoFactorAttemptCount(It.IsAny<Guid>()), Times.Once);
        repository.Verify(repo => repo.MarkSessionTwoFactorVerified(It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Never);
    }

    private static UserLoginService CreateService(
        Mock<IUserLoginRepository> repository,
        Mock<IUserService> userService,
        Mock<ISendEmailService> sendEmailService)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtConfig:Key"] = "12345678901234567890123456789012",
                ["JwtConfig:ExpiryMinutes"] = "60",
                ["Authentication:TwoFactorCodeExpiryMinutes"] = "30",
                ["Authentication:TwoFactorMaxAttempts"] = "5"
            })
            .Build();

        return new UserLoginService(
            configuration,
            repository.Object,
            userService.Object,
            sendEmailService.Object);
    }

    private static string Hash(string value)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
