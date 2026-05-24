using Application.Users.Contracts;
using Application.Users.Implementation;
using Email;
using Microsoft.Extensions.Configuration;
using Moq;
using Users.Contracts;
using Users.Repository;

namespace Courses.Test;

public class ExtendedUserInformationServiceTests
{
    [Fact]
    public async Task GetExtendedUserInformation_MasksSin()
    {
        var detail = CreateDetail();
        var repository = new Mock<IExtendedUserInformationRepository>();
        repository
            .Setup(repo => repo.GetExtendedUserInformation(detail.UserId))
            .ReturnsAsync(detail);

        var service = CreateService(repository.Object);

        var response = await service.GetExtendedUserInformation(detail.UserId);

        Assert.NotNull(response);
        Assert.Equal("***-***-789", response!.SIN);
    }

    [Fact]
    public async Task GetFamilyExtendedUserInformation_MasksSin()
    {
        var detail = CreateDetail();
        var repository = new Mock<IExtendedUserInformationRepository>();
        repository
            .Setup(repo => repo.GetFamilyExtendedUserInformation(detail.FamilyId))
            .ReturnsAsync(detail);

        var service = CreateService(repository.Object);

        var response = await service.GetFamilyExtendedUserInformation(detail.FamilyId);

        Assert.NotNull(response);
        Assert.Equal("***-***-789", response!.SIN);
    }

    [Fact]
    public async Task AddExtendedUserInformation_MasksSinInResponse()
    {
        var detail = CreateDetail();
        var request = new AddExtendedUserInformationInternal
        {
            UserId = detail.UserId,
            FamilyId = detail.FamilyId,
            AddressId = detail.AddressId,
            SIN = detail.SIN,
            IsActiveTaxCreditRecipient = detail.IsActiveTaxCreditRecipient
        };

        var repository = new Mock<IExtendedUserInformationRepository>();
        repository
            .Setup(repo => repo.GetFamilyExtendedUserInformation(detail.FamilyId))
            .ReturnsAsync((ExtendedUserInformationDetail)null!);
        repository
            .Setup(repo => repo.GetExtendedUserInformation(detail.UserId))
            .ReturnsAsync((ExtendedUserInformationDetail)null!);
        repository
            .Setup(repo => repo.AddExtendedUserInformation(It.IsAny<ExtendedUserInformationDetail>()))
            .ReturnsAsync(detail);

        var service = CreateService(repository.Object, CreateUserRepository(detail));

        var response = await service.AddExtendedUserInformation(request);

        Assert.NotNull(response);
        Assert.Equal("***-***-789", response!.SIN);
    }

    [Fact]
    public async Task UpdateExtendedUserInformation_MasksSinInResponse()
    {
        var detail = CreateDetail();
        var repository = new Mock<IExtendedUserInformationRepository>();
        repository
            .Setup(repo => repo.UpdateExtendedUserInformation(It.IsAny<AddExtendedUserInformationInternal>()))
            .ReturnsAsync(detail);

        var service = CreateService(repository.Object);

        var response = await service.UpdateExtendedUserInformation(new AddExtendedUserInformationInternal
        {
            UserId = detail.UserId,
            AddressId = detail.AddressId
        });

        Assert.NotNull(response);
        Assert.Equal("***-***-789", response!.SIN);
    }

    private static ExtendedUserInformationService CreateService(
        IExtendedUserInformationRepository repository,
        IUserRepository? userRepository = null,
        ISendEmailService? sendEmailService = null)
    {
        return new ExtendedUserInformationService(
            Mock.Of<IConfiguration>(),
            sendEmailService ?? Mock.Of<ISendEmailService>(),
            repository,
            userRepository ?? Mock.Of<IUserRepository>());
    }

    private static IUserRepository CreateUserRepository(ExtendedUserInformationDetail detail)
    {
        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetAllFamilyUsersInformation(detail.FamilyId, true))
            .ReturnsAsync(new List<UserInformation>
            {
                new()
                {
                    UserId = detail.UserId,
                    FamilyId = detail.FamilyId,
                    FirstName = "Test",
                    LastName = "User",
                    Email = "test@example.com"
                }
            });

        return repository.Object;
    }

    private static ExtendedUserInformationDetail CreateDetail()
    {
        return new ExtendedUserInformationDetail
        {
            UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            FamilyId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            AddressId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            SIN = "123-456-789",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedOn = DateTime.UtcNow,
            IsActiveTaxCreditRecipient = true
        };
    }
}
