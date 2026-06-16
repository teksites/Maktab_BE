using Courses.Implementation.Services;
using Courses.Repository;
using Moq;
using AddCourseGroupPreRequisite = MaktabDataContracts.Requests.Course.AddCourseGroupPreRequisite;
using CourseGroupPreRequisiteResponse = MaktabDataContracts.Responses.Course.CourseGroupPreRequisiteResponse;

namespace Courses.Test;

public class CourseGroupPreRequisiteServiceTests
{
    [Fact]
    public async Task Add_DelegatesToRepository()
    {
        var request = new AddCourseGroupPreRequisite
        {
            CourseGroupPreRequisiteId = Guid.NewGuid(),
            CourseGroupId = Guid.NewGuid(),
            PreRequisiteCourseGroupId = Guid.NewGuid()
        };
        var expected = CreateResponse(request.CourseGroupPreRequisiteId, request.CourseGroupId, request.PreRequisiteCourseGroupId);

        var repository = new Mock<ICourseGroupPreRequisiteRepository>();
        repository
            .Setup(repo => repo.Add(request))
            .ReturnsAsync(expected);

        var service = new CourseGroupPreRequisiteService(repository.Object);

        var result = await service.Add(request);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GetByCourseGroup_DelegatesToRepository()
    {
        var courseGroupId = Guid.NewGuid();
        var expected = new[]
        {
            CreateResponse(Guid.NewGuid(), courseGroupId, Guid.NewGuid())
        };

        var repository = new Mock<ICourseGroupPreRequisiteRepository>();
        repository
            .Setup(repo => repo.GetByCourseGroup(courseGroupId, false))
            .ReturnsAsync(expected);

        var service = new CourseGroupPreRequisiteService(repository.Object);

        var result = await service.GetByCourseGroup(courseGroupId, false);

        Assert.Equal(expected, result);
    }

    private static CourseGroupPreRequisiteResponse CreateResponse(Guid id, Guid courseGroupId, Guid prerequisiteGroupId)
        => new()
        {
            CourseGroupPreRequisiteId = id,
            CourseGroupId = courseGroupId,
            PreRequisiteCourseGroupId = prerequisiteGroupId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedOn = DateTime.UtcNow
        };
}
