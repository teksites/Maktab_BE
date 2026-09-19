using Maktab.Attributes;
using Maktab.Controllers;
using MaktabDataContracts.Enums;
using System.Reflection;

namespace Courses.Test;

public class ChildrenControllerAuthorizationTests
{
    [Fact]
    public void UpsertChildEducationalProfile_RequiresNormalUserRole()
    {
        var action = typeof(ChildrenController).GetMethod(nameof(ChildrenController.UpsertChildEducationalProfile));
        var authorization = action!.GetCustomAttribute<ApiAuthorizeAttribute>();
        var requiredRole = typeof(ApiAuthorizeAttribute)
            .GetField("_requiredRole", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(authorization);

        Assert.Equal(UserRoleType.Normal, requiredRole);
    }
}
