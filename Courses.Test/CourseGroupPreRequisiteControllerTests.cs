using Courses.Services;
using Moq;
using AddCourseGroupPreRequisite = MaktabDataContracts.Requests.Course.AddCourseGroupPreRequisite;
using CourseGroupPreRequisiteResponse = MaktabDataContracts.Responses.Course.CourseGroupPreRequisiteResponse;

namespace Courses.Test;

public class CourseGroupPreRequisiteControllerTests
{
    [Fact]
    public async Task Add_DelegatesToService()
    {
        var request = new AddCourseGroupPreRequisite
        {
            CourseGroupPreRequisiteId = Guid.NewGuid(),
            CourseGroupId = Guid.NewGuid(),
            PreRequisiteCourseGroupId = Guid.NewGuid()
        };
        var expected = new CourseGroupPreRequisiteResponse
        {
            CourseGroupPreRequisiteId = request.CourseGroupPreRequisiteId,
            CourseGroupId = request.CourseGroupId,
            PreRequisiteCourseGroupId = request.PreRequisiteCourseGroupId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedOn = DateTime.UtcNow
        };

        var service = new Mock<ICourseGroupPreRequisiteService>();
        service
            .Setup(instance => instance.Add(request))
            .ReturnsAsync(expected);

        var controller = new CourseGroupPreRequisiteController(service.Object);

        var result = await controller.Add(request);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GetByCourseGroup_DelegatesToService()
    {
        var courseGroupId = Guid.NewGuid();
        var expected = new[]
        {
            new CourseGroupPreRequisiteResponse
            {
                CourseGroupPreRequisiteId = Guid.NewGuid(),
                CourseGroupId = courseGroupId,
                PreRequisiteCourseGroupId = Guid.NewGuid(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedOn = DateTime.UtcNow
            }
        };

        var service = new Mock<ICourseGroupPreRequisiteService>();
        service
            .Setup(instance => instance.GetByCourseGroup(courseGroupId, false))
            .ReturnsAsync(expected);

        var controller = new CourseGroupPreRequisiteController(service.Object);

        var result = await controller.GetByCourseGroup(courseGroupId, false);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task GetByEnrollment_DelegatesToService()
    {
        var enrollmentId = Guid.NewGuid();
        var expected = new[]
        {
            new CourseGroupPreRequisiteResponse
            {
                CourseGroupPreRequisiteId = Guid.NewGuid(),
                CourseGroupId = Guid.NewGuid(),
                PreRequisiteCourseGroupId = Guid.NewGuid(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedOn = DateTime.UtcNow
            }
        };

        var service = new Mock<ICourseGroupPreRequisiteService>();
        service
            .Setup(instance => instance.GetByEnrollment(enrollmentId, false))
            .ReturnsAsync(expected);

        var controller = new CourseGroupPreRequisiteController(service.Object);

        var result = await controller.GetByEnrollment(enrollmentId, false);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task Update_DelegatesToService()
    {
        var prerequisiteId = Guid.NewGuid();
        var request = new AddCourseGroupPreRequisite
        {
            CourseGroupPreRequisiteId = prerequisiteId,
            CourseGroupId = Guid.NewGuid(),
            PreRequisiteCourseGroupId = Guid.NewGuid()
        };

        var service = new Mock<ICourseGroupPreRequisiteService>();
        service
            .Setup(instance => instance.Update(prerequisiteId, request))
            .ReturnsAsync(true);

        var controller = new CourseGroupPreRequisiteController(service.Object);

        var result = await controller.Update(prerequisiteId, request);

        Assert.True(result);
    }
}
