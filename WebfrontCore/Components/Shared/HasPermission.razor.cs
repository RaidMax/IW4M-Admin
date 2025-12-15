using Microsoft.AspNetCore.Components;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using WebfrontCore.Permissions;
using WebfrontCore.Services;

namespace WebfrontCore.Components.Shared;

public partial class HasPermission
{
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required ApplicationConfiguration Config { get; set; }
    [Parameter] public string Entity { get; set; }
    [Parameter] public string RequiredPermission { get; set; }
    [Parameter] public RenderFragment ChildContent { get; set; }
    private bool Authorized => CheckPermission();

    private bool CheckPermission()
    {
        if (AppState.User == null)
        {
            // Not logged in - deny access TODO
            return false;
        }

        // Parse the entity and permission from string parameters
        if (!Enum.TryParse<WebfrontEntity>(Entity, out var entityEnum))
        {
            return false;
        }

        if (!Enum.TryParse<WebfrontPermission>(RequiredPermission, out var permissionEnum))
        {
            return false;
        }

        // Use the extension method from SharedLibraryCore.Utilities
        return Config.HasPermission(AppState.User.Level, entityEnum, permissionEnum);
    }
}
