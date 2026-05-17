using AppConfigurations.Implementation.Services;
using AppConfigurations.Repository;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Requests.Configs;
using MaktabDataContracts.Responses.Configs;
using Moq;

namespace Courses.Test;

public class AppConfigServiceTests
{
    [Fact]
    public async Task AddAppConfig_DelegatesToRepository()
    {
        var request = CreateAddRequest();
        var expected = CreateResponse();
        var repository = new Mock<IAppConfigRepository>();
        repository
            .Setup(repo => repo.AddAppConfig(request))
            .ReturnsAsync(expected);

        var service = new AppConfigService(repository.Object);

        var result = await service.AddAppConfig(request);

        Assert.Same(expected, result);
        repository.Verify(repo => repo.AddAppConfig(request), Times.Once);
    }

    [Fact]
    public async Task GetAppConfig_DelegatesToRepository()
    {
        var appConfigId = Guid.NewGuid();
        var expected = CreateResponse(appConfigId);
        var repository = new Mock<IAppConfigRepository>();
        repository
            .Setup(repo => repo.GetAppConfig(appConfigId))
            .ReturnsAsync(expected);

        var service = new AppConfigService(repository.Object);

        var result = await service.GetAppConfig(appConfigId);

        Assert.Same(expected, result);
        repository.Verify(repo => repo.GetAppConfig(appConfigId), Times.Once);
    }

    [Fact]
    public async Task GetAllAppConfigs_DelegatesToRepository()
    {
        var expected = new[]
        {
            CreateResponse(),
            CreateResponse(Guid.NewGuid(), ConfigurationType.Other)
        };
        var repository = new Mock<IAppConfigRepository>();
        repository
            .Setup(repo => repo.GetAllAppConfigs(false))
            .ReturnsAsync(expected);

        var service = new AppConfigService(repository.Object);

        var result = await service.GetAllAppConfigs(false);

        Assert.Equal(expected, result);
        repository.Verify(repo => repo.GetAllAppConfigs(false), Times.Once);
    }

    [Fact]
    public async Task GetLatestAppConfigByType_DelegatesToRepository()
    {
        var expected = CreateResponse(Guid.NewGuid(), ConfigurationType.EmailAttachments);
        var repository = new Mock<IAppConfigRepository>();
        repository
            .Setup(repo => repo.GetLatestAppConfigByType(ConfigurationType.EmailAttachments, true))
            .ReturnsAsync(expected);

        var service = new AppConfigService(repository.Object);

        var result = await service.GetLatestAppConfigByType(ConfigurationType.EmailAttachments, true);

        Assert.Same(expected, result);
        repository.Verify(repo => repo.GetLatestAppConfigByType(ConfigurationType.EmailAttachments, true), Times.Once);
    }

    [Fact]
    public async Task UpdateAppConfig_DelegatesToRepository()
    {
        var appConfigId = Guid.NewGuid();
        var request = CreateUpdateRequest();
        var repository = new Mock<IAppConfigRepository>();
        repository
            .Setup(repo => repo.UpdateAppConfig(appConfigId, request))
            .ReturnsAsync(true);

        var service = new AppConfigService(repository.Object);

        var result = await service.UpdateAppConfig(appConfigId, request);

        Assert.True(result);
        repository.Verify(repo => repo.UpdateAppConfig(appConfigId, request), Times.Once);
    }

    [Fact]
    public async Task DeleteAppConfig_DelegatesToRepository()
    {
        var appConfigId = Guid.NewGuid();
        var repository = new Mock<IAppConfigRepository>();
        repository
            .Setup(repo => repo.DeleteAppConfig(appConfigId, true))
            .ReturnsAsync(true);

        var service = new AppConfigService(repository.Object);

        var result = await service.DeleteAppConfig(appConfigId, hardDelete: true);

        Assert.True(result);
        repository.Verify(repo => repo.DeleteAppConfig(appConfigId, true), Times.Once);
    }

    private static AddAppConfigRequest CreateAddRequest()
        => new()
        {
            Content = "abc123",
            ConfigurationType = ConfigurationType.AddressKey,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedOn = DateTime.UtcNow
        };

    private static UpdateAppConfigRequest CreateUpdateRequest()
        => new()
        {
            Content = "xyz789",
            ConfigurationType = ConfigurationType.Other,
            IsActive = false,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedOn = DateTime.UtcNow
        };

    private static AppConfigResponse CreateResponse(Guid? id = null, ConfigurationType configurationType = ConfigurationType.AddressKey)
        => new()
        {
            Id = id ?? Guid.NewGuid(),
            Content = "payload",
            ConfigurationType = configurationType,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedOn = DateTime.UtcNow
        };
}
