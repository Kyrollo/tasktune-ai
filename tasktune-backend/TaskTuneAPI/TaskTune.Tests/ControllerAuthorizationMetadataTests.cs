using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using TaskTune.API.Controllers;
using Xunit;

namespace TaskTune.Tests;

public sealed class ControllerAuthorizationMetadataTests
{
    private static readonly HashSet<string> ExplicitAnonymousActions = new(StringComparer.Ordinal)
    {
        "AuthController.Login",
        "AuthController.RefreshToken",
        "AuthController.ForgotPassword",
        "AuthController.ResetPassword",
        "AuthController.Register",
        "HealthCheckController.Live",
        "HealthCheckController.Ready",
        "HealthCheckController.Check"
    };

    [Fact]
    public void EveryApiActionDeclaresAuthorizationOrExplicitAnonymousAccess()
    {
        var apiControllers = typeof(AuthController).Assembly.GetTypes()
            .Where(type =>
                !type.IsAbstract &&
                typeof(ControllerBase).IsAssignableFrom(type) &&
                type.GetCustomAttribute<ApiControllerAttribute>(inherit: true) is not null)
            .ToArray();

        Assert.NotEmpty(apiControllers);
        var violations = new List<string>();

        foreach (var controller in apiControllers)
        {
            var controllerRequiresAuth = controller
                .GetCustomAttributes(inherit: true)
                .OfType<IAuthorizeData>()
                .Any();
            var controllerAllowsAnonymous = controller
                .GetCustomAttributes(inherit: true)
                .OfType<AllowAnonymousAttribute>()
                .Any();

            var actions = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>().Any());

            foreach (var action in actions)
            {
                var methodAttributes = action.GetCustomAttributes(inherit: true).ToArray();
                var methodRequiresAuth = methodAttributes.OfType<IAuthorizeData>().Any();
                var methodAllowsAnonymous = methodAttributes.OfType<AllowAnonymousAttribute>().Any();

                if (!(controllerRequiresAuth || methodRequiresAuth ||
                      controllerAllowsAnonymous || methodAllowsAnonymous))
                    violations.Add($"{controller.Name}.{action.Name}");
            }
        }

        Assert.True(violations.Count == 0,
            "API actions must declare [Authorize] (class or method) or an explicit [AllowAnonymous]: " +
            string.Join(", ", violations));
    }

    [Fact]
    public void AnonymousApiActionsMatchTheReviewedPublicEndpointAllowlist()
    {
        var actual = new HashSet<string>(StringComparer.Ordinal);
        var apiControllers = typeof(AuthController).Assembly.GetTypes()
            .Where(type =>
                !type.IsAbstract &&
                typeof(ControllerBase).IsAssignableFrom(type) &&
                type.GetCustomAttribute<ApiControllerAttribute>(inherit: true) is not null);

        foreach (var controller in apiControllers)
        {
            var classAllowsAnonymous = controller
                .GetCustomAttributes(inherit: true)
                .OfType<AllowAnonymousAttribute>()
                .Any();
            var actions = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>().Any());

            foreach (var action in actions)
            {
                var methodAllowsAnonymous = action
                    .GetCustomAttributes(inherit: true)
                    .OfType<AllowAnonymousAttribute>()
                    .Any();
                if (classAllowsAnonymous || methodAllowsAnonymous)
                    actual.Add($"{controller.Name}.{action.Name}");
            }
        }

        Assert.Equal(ExplicitAnonymousActions.OrderBy(value => value),
            actual.OrderBy(value => value));
    }
    
    [Fact]
    public void CommunicationOutboxOperatorActionsRequireCeoOrAdmin()
    {
        var controller = typeof(CommunicationsController);
        var expected = new[]
        {
            nameof(CommunicationsController.GetOutboxMetrics),
            nameof(CommunicationsController.GetDeadLetterOutbox),
            nameof(CommunicationsController.ReplayDeadLetter)
        };

        foreach (var actionName in expected)
        {
            var action = controller.GetMethod(actionName);
            Assert.NotNull(action);
            var authorization = action!.GetCustomAttributes<AuthorizeAttribute>(inherit: true).SingleOrDefault();
            Assert.NotNull(authorization);
            Assert.Equal("CEO,Admin", authorization!.Roles);
            Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true));
        }
    }

    [Fact]
    public void TenantUsageControllerRequiresAuthentication()
    {
        var controller = typeof(TenantUsageController);
        Assert.NotEmpty(controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true));
        Assert.Null(controller.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true));
    }

    [Fact]
    public void TenantEntitlementsControllerRequiresAuthentication()
    {
        var controller = typeof(TenantEntitlementsController);
        Assert.NotEmpty(controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true));
        Assert.Null(controller.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true));
    }

    [Fact]
    public void PlatformPlansAndSubscriptionsRequirePlatformAdminPolicy()
    {
        foreach (var controller in new[] { typeof(PlatformPlansController), typeof(PlatformSubscriptionsController) })
        {
            var authorization = controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).SingleOrDefault();
            Assert.NotNull(authorization);
            Assert.Equal("PlatformAdmin", authorization!.Policy);
            Assert.Null(controller.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true));
        }
    }

    [Fact]
    public void PlatformTenantControllerRequiresThePlatformAdminPolicy()
    {
        var controller = typeof(PlatformTenantsController);
        var authorization = controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).SingleOrDefault();
        Assert.NotNull(authorization);
        Assert.Equal("PlatformAdmin", authorization!.Policy);
        Assert.Null(controller.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true));
    }

    [Fact]
    public void NotificationsControllerRequiresAuthenticationAndHasNoAnonymousOverrides()
    {
        var controller = typeof(NotificationsController);
        Assert.NotEmpty(controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true));

        var actions = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>().Any());
        Assert.NotEmpty(actions);
        Assert.All(actions, action =>
            Assert.Empty(action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true)));
    }
}
