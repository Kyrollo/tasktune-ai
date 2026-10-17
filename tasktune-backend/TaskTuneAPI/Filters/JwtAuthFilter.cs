using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using TaskTuneAPI.Helpers;

namespace TaskTuneAPI.Filters
{
    public class JwtAuthFilter : AuthorizationFilterAttribute
    {
        private readonly byte[] _allowedRoles;

        public JwtAuthFilter(params byte[] allowedRoles)
        {
            _allowedRoles = allowedRoles;
        }

        public override void OnAuthorization(HttpActionContext actionContext)
        {
            var authHeader = actionContext.Request.Headers.Authorization;

            if (authHeader == null || authHeader.Scheme != "Bearer" || string.IsNullOrEmpty(authHeader.Parameter))
            {
                actionContext.Response = actionContext.Request.CreateErrorResponse(
                    HttpStatusCode.Unauthorized, "Missing or invalid authorization header");
                return;
            }

            try
            {
                var principal = JwtHelper.ValidateToken(authHeader.Parameter);
                Thread.CurrentPrincipal = principal;

                if (System.Web.HttpContext.Current != null)
                    System.Web.HttpContext.Current.User = principal;

                if (_allowedRoles != null && _allowedRoles.Length > 0)
                {
                    var roleClaim = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
                    if (roleClaim == null || !byte.TryParse(roleClaim.Value, out byte userRole) || !_allowedRoles.Contains(userRole))
                    {
                        actionContext.Response = actionContext.Request.CreateErrorResponse(
                            HttpStatusCode.Forbidden, "Insufficient permissions");
                        return;
                    }
                }
            }
            catch (Exception)
            {
                actionContext.Response = actionContext.Request.CreateErrorResponse(
                    HttpStatusCode.Unauthorized, "Invalid or expired token");
            }
        }
    }

    // Convenience attribute that accepts all authenticated users
    public class JwtAuthorizeAttribute : JwtAuthFilter
    {
        public JwtAuthorizeAttribute() : base() { }
        public JwtAuthorizeAttribute(params byte[] roles) : base(roles) { }
    }
}