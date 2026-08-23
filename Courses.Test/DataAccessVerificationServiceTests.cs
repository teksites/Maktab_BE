using Application.Users.Contracts;
using Application.Users.Implementation;
using MaktabDataContracts.Enums;
using Moq;
using Users.Services;

namespace Courses.Test;

public class DataAccessVerificationServiceTests
{
    [Fact]
    public async Task GetSessionAccessContext_ReturnsNull_ForPendingTwoFactorSession()
    {
        var sessionId = Guid.NewGuid();
        var loginService = new Mock<IUserLoginService>();
        loginService
            .Setup(service => service.GetSessionAuthenticationState(sessionId))
            .ReturnsAsync(new SessionAuthenticationState
            {
                SessionId = sessionId,
                UserId = Guid.NewGuid(),
                FamilyId = Guid.NewGuid(),
                IsActive = true,
                RequiresTwoFactorVerification = true,
                IsTwoFactorVerified = false
            });

        var userService = new Mock<IUserService>();
        var service = new DataAccessVerificationService(loginService.Object, userService.Object);

        var result = await service.GetSessionAccessContext(sessionId);

        Assert.Null(result);
        userService.Verify(service => service.GetUserInformation(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetSessionAccessContext_ReturnsContext_ForVerifiedSession()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();

        var loginService = new Mock<IUserLoginService>();
        loginService
            .Setup(service => service.GetSessionAuthenticationState(sessionId))
            .ReturnsAsync(new SessionAuthenticationState
            {
                SessionId = sessionId,
                UserId = userId,
                FamilyId = familyId,
                IsActive = true,
                RequiresTwoFactorVerification = true,
                IsTwoFactorVerified = true,
                TwoFactorVerifiedOn = DateTime.UtcNow
            });

        var userService = new Mock<IUserService>();
        userService
            .Setup(service => service.GetUserInformation(userId))
            .ReturnsAsync(new MaktabDataContracts.Responses.Users.UserInformationResponse
            {
                UserId = userId,
                FamilyId = familyId
            });
        userService
            .Setup(service => service.GetUserRoles(userId))
            .ReturnsAsync(UserRoleType.Normal);

        var service = new DataAccessVerificationService(loginService.Object, userService.Object);

        var result = await service.GetSessionAccessContext(sessionId);

        Assert.NotNull(result);
        Assert.Equal(sessionId, result!.SessionId);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(familyId, result.FamilyId);
        Assert.True(result.RequiresTwoFactorVerification);
        Assert.True(result.IsTwoFactorVerified);
        Assert.Equal(UserRoleType.Normal, result.UserRoles);
    }
}
