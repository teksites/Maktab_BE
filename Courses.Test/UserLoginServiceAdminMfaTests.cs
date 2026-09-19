using Email;
using Microsoft.Extensions.Configuration;
using Moq;
using MaktabDataContracts.Enums;
using Users.Contracts;
using Users.Implementation.Services;
using Users.Repository;
using Users.Services;
using AddSession = InternalContracts.AddSession;
using AddSessionTwoFactorCode = InternalContracts.AddSessionTwoFactorCode;

namespace Courses.Test;

public class UserLoginServiceAdminMfaTests
{
    [Theory]
    [InlineData(UserRoleType.Admin)]
    [InlineData(UserRoleType.SuperUser)]
    public async Task Authenticate_WhenAdministrativeMfaEnabled_UsesEightHourCodeExpiry(UserRoleType role)
    {
        var userId = Guid.NewGuid();
        AddSessionTwoFactorCode? capturedCode = null;
        var repository = new Mock<IUserLoginRepository>();
        repository.Setup(repo => repo.GetSessionByUserId(userId)).ReturnsAsync(Guid.Empty);
        repository.Setup(repo => repo.LogInSession(It.IsAny<AddSession>())).ReturnsAsync(true);
        repository.Setup(repo => repo.DeactivateSessionTwoFactorCodes(It.IsAny<Guid>())).ReturnsAsync(true);
        repository
            .Setup(repo => repo.AddSessionTwoFactorCode(It.IsAny<AddSessionTwoFactorCode>()))
            .Callback<AddSessionTwoFactorCode>(code => capturedCode = code)
            .ReturnsAsync(true);

        var users = new Mock<IUserService>();
        users.Setup(service => service.GetUserInformation("super-user", "Password123", false))
            .ReturnsAsync(new MaktabDataContracts.Responses.Users.UserInformationResponse
            {
                UserId = userId,
                FamilyId = Guid.NewGuid(),
                Email = "super@example.com",
                FirstName = "Super",
                LastName = "User",
                IfTempUser = false,
                IsMultiFactorLoginEnabled = true
            });
        users.Setup(service => service.GetUserRoles(userId)).ReturnsAsync(role);

        var emails = new Mock<ISendEmailService>();
        emails.Setup(service => service.SendEmail(It.IsAny<EmailData>())).ReturnsAsync(true);
        var beforeAuthentication = DateTime.UtcNow;
        var service = CreateService(repository, users, emails);

        var response = await service.Authenticate("super-user", "Password123", "127.0.0.1");

        Assert.NotNull(response);
        Assert.NotNull(capturedCode);
        Assert.InRange(capturedCode!.ExpiresOn, beforeAuthentication.AddHours(8).AddSeconds(-1), DateTime.UtcNow.AddHours(8).AddSeconds(1));
    }

    private static UserLoginService CreateService(
        Mock<IUserLoginRepository> repository,
        Mock<IUserService> users,
        Mock<ISendEmailService> emails)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtConfig:Key"] = "12345678901234567890123456789012",
                ["JwtConfig:ExpiryMinutes"] = "60",
                ["Authentication:TwoFactorCodeExpiryMinutes"] = "30",
                ["Authentication:AdminTwoFactorCodeExpiryMinutes"] = "480"
            })
            .Build();

        return new UserLoginService(configuration, repository.Object, users.Object, emails.Object);
    }
}
