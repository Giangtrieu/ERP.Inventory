using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ERP.Inventory.Web.Middleware    // Namespace theo project Web
{
    public class SuperAdminAuthorizationHandler : AuthorizationHandler<RolesAuthorizationRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            RolesAuthorizationRequirement requirement)
        {
            // Nếu là SuperAdmin thì bypass hết
            if (context.User.HasClaim(c => c.Type == "AuthMode" && c.Value == "Super"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Logic user thường
            var userRoles = context.User.FindAll(ClaimTypes.Role)
                                       .Select(c => c.Value)
                                       .ToList();

            if (requirement.AllowedRoles.Any(role => userRoles.Contains(role)))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}