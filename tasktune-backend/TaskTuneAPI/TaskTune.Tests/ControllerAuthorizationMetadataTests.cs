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
}
