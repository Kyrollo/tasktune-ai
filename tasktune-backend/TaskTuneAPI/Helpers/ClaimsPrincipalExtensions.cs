using System.Linq;
using System.Security.Claims;

namespace TaskTuneAPI.Helpers
{
    public static class ClaimsPrincipalExtensions
    {
        public static int GetUserId(this ClaimsPrincipal principal)
        {
            var claim = principal.Claims.FirstOrDefault(c => c.Type == "userId");
            return claim != null ? int.Parse(claim.Value) : 0;
        }

        public static int GetCompanyId(this ClaimsPrincipal principal)
        {
            var claim = principal.Claims.FirstOrDefault(c => c.Type == "companyId");
            return claim != null ? int.Parse(claim.Value) : 0;
        }

        public static byte GetRole(this ClaimsPrincipal principal)
        {
            var claim = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
            return claim != null ? byte.Parse(claim.Value) : (byte)0;
        }
    }
}