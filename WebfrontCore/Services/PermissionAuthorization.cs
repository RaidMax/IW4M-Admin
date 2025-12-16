using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using SharedLibraryCore.Configuration;
using SharedLibraryCore;
using WebfrontCore.Permissions;

namespace WebfrontCore.Services;

public class PermissionRequirement(WebfrontEntity entity, WebfrontPermission permission) : IAuthorizationRequirement
{
    public WebfrontEntity Entity { get; } = entity;
    public WebfrontPermission Permission { get; } = permission;
}

public class PermissionAuthorizationHandler(ApplicationConfiguration config)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        // The Role claim contains the Permission level (e.g. "Administrator", "Trusted")
        var levelClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.Role);
        if (levelClaim == null || !Enum.TryParse<Data.Models.Client.EFClient.Permission>(levelClaim.Value, out var level))
        {
            return Task.CompletedTask;
        }

        if (config.HasPermission(level, requirement.Entity, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith("Permissions.", StringComparison.OrdinalIgnoreCase))
        {
            return await base.GetPolicyAsync(policyName);
        }

        // Format is Permissions.{Entity}.{Permission}
        // Example: Permissions.PrivilegedClientsPage.Read
        var parts = policyName.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        if (!Enum.TryParse<WebfrontEntity>(parts[1], out var entity) ||
            !Enum.TryParse<WebfrontPermission>(parts[2], out var permission))
        {
            return null;
        }

        var policy = new AuthorizationPolicyBuilder();
        policy.AddRequirements(new PermissionRequirement(entity, permission));
        return policy.Build();
    }
}
