using Application.Users.Contracts;
using Application.Users.Implementation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Maktab.Controllers;
using Moq;
using MaktabDataContracts.Enums;
using MaktabDataContracts.Models;
using MaktabDataContracts.Requests.OtherContacts;
using MaktabDataContracts.Responses.OtherContacts;
using Users.Repository;
using Users.Services;

namespace Courses.Test;

public class OtherContactsServiceTests
{
    [Theory]
    [InlineData(Relationship.Mother)]
    [InlineData(Relationship.Father)]
    [InlineData(Relationship.Guardian)]
    public async Task AddOtherContact_WithFamilyRelationship_Rejects(Relationship relationship)
    {
        var repository = new Mock<IOtherContactsRepository>(MockBehavior.Strict);
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddOtherContact(new AddOtherContact
        {
            FamilyId = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "User",
            Phone = "1234567890",
            ContactType = ContactType.Emergency,
            Relationship = relationship
        }));

        Assert.Equal("Other contacts cannot use Mother, Father, or Guardian relationship. Please add them as family users instead.", exception.Message);
        repository.Verify(
            instance => instance.AddOtherContact(It.IsAny<OtherContactInformation>()),
            Times.Never);
    }

    [Theory]
    [InlineData(Relationship.Mother)]
    [InlineData(Relationship.Father)]
    [InlineData(Relationship.Guardian)]
    public async Task UpdateOtherContact_WithFamilyRelationship_Rejects(Relationship relationship)
    {
        var contactId = Guid.NewGuid();
        var existing = new OtherContactInformation
        {
            ContactId = contactId,
            FamilyId = Guid.NewGuid(),
            FirstName = "Existing",
            LastName = "Contact",
            Phone = "1234567890",
            ContactType = ContactType.Emergency,
            Relationship = Relationship.Relative,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedOn = DateTime.UtcNow.AddDays(-1)
        };

        var repository = new Mock<IOtherContactsRepository>(MockBehavior.Strict);
        repository
            .Setup(instance => instance.GetOtherContact(contactId))
            .ReturnsAsync(existing);

        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateOtherContact(new UpdateOtherContact
        {
            ContactId = contactId,
            FirstName = existing.FirstName,
            LastName = existing.LastName,
            Phone = existing.Phone,
            ContactType = existing.ContactType,
            Relationship = relationship
        }));

        Assert.Equal("Other contacts cannot use Mother, Father, or Guardian relationship. Please add them as family users instead.", exception.Message);
        repository.Verify(instance => instance.GetOtherContact(contactId), Times.Once);
        repository.Verify(
            instance => instance.UpdateOtherContact(It.IsAny<OtherContactInformation>()),
            Times.Never);
    }

    [Fact]
    public async Task AddOtherContact_ControllerReturnsBadRequestForRejectedRelationship()
    {
        var service = new Mock<IOtherContactsService>();
        service
            .Setup(instance => instance.AddOtherContact(It.IsAny<AddOtherContact>()))
            .ThrowsAsync(new InvalidOperationException("Other contacts cannot use Mother, Father, or Guardian relationship. Please add them as family users instead."));

        var controller = new OtherContactsController(
            service.Object,
            Mock.Of<IDataAccessVerificationService>(),
            Mock.Of<ILogger<OtherContactsController>>());

        var result = await controller.AddUserAddress(Guid.NewGuid(), new AddOtherContact
        {
            FamilyId = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "User",
            Phone = "1234567890",
            ContactType = ContactType.Emergency,
            Relationship = Relationship.Mother
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var apiResult = Assert.IsType<ApiResult>(badRequest.Value);
        var error = Assert.Single(apiResult.Errors);
        Assert.Equal("Other contacts cannot use Mother, Father, or Guardian relationship. Please add them as family users instead.", error.ErrorMessage);
    }

    [Fact]
    public async Task UpdateOtherContact_ControllerReturnsBadRequestForRejectedRelationship()
    {
        var contactId = Guid.NewGuid();
        var existing = new OtherContactResponse
        {
            ContactId = contactId,
            FamilyId = Guid.NewGuid(),
            FirstName = "Existing",
            LastName = "Contact",
            Phone = "1234567890",
            ContactType = ContactType.Emergency,
            Relationship = Relationship.Relative,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedOn = DateTime.UtcNow.AddDays(-1)
        };

        var service = new Mock<IOtherContactsService>();
        service
            .Setup(instance => instance.GetOtherContact(contactId))
            .ReturnsAsync(existing);
        service
            .Setup(instance => instance.UpdateOtherContact(It.IsAny<UpdateOtherContact>()))
            .ThrowsAsync(new InvalidOperationException("Other contacts cannot use Mother, Father, or Guardian relationship. Please add them as family users instead."));

        var controller = new OtherContactsController(
            service.Object,
            CreateDataAccessVerificationService(existing.FamilyId),
            Mock.Of<ILogger<OtherContactsController>>());

        controller.ControllerContext = new ControllerContext();
        controller.ControllerContext.HttpContext = new DefaultHttpContext();
        controller.Request.Headers["Session_Info"] = Guid.NewGuid().ToString();

        var result = await controller.UpdateOtherContact(new UpdateOtherContact
        {
            ContactId = contactId,
            FirstName = existing.FirstName,
            LastName = existing.LastName,
            Phone = existing.Phone,
            ContactType = existing.ContactType,
            Relationship = Relationship.Father
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var apiResult = Assert.IsType<ApiResult>(badRequest.Value);
        var error = Assert.Single(apiResult.Errors);
        Assert.Equal("Other contacts cannot use Mother, Father, or Guardian relationship. Please add them as family users instead.", error.ErrorMessage);
    }

    private static OtherContactsService CreateService(Mock<IOtherContactsRepository> repository)
    {
        return new OtherContactsService(new ConfigurationBuilder().Build(), repository.Object);
    }

    private static IDataAccessVerificationService CreateDataAccessVerificationService(Guid familyId)
    {
        var service = new Mock<IDataAccessVerificationService>();
        service
            .Setup(instance => instance.GetSessionAccessContext(It.IsAny<Guid>()))
            .ReturnsAsync(new SessionAccessContext
            {
                UserId = Guid.NewGuid(),
                FamilyId = familyId,
                UserRoles = UserRoleType.Normal
            });
        service
            .Setup(instance => instance.HasElevatedAccess(It.IsAny<UserRoleType>()))
            .Returns(false);

        return service.Object;
    }
}
