using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Data.Models.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using WebfrontCore.Permissions;

namespace WebfrontCore.TagHelpers;

[HtmlTargetElement("has-permission")]
public class HasPermission : TagHelper
{
    [HtmlAttributeName("entity")] public WebfrontEntity Entity { get; set; }

    [HtmlAttributeName("required-permission")]
    public WebfrontPermission Permission { get; set; }

    private readonly IDictionary<string, List<string>> _permissionSets;
    private readonly IHttpContextAccessor _contextAccessor;

    public HasPermission(ApplicationConfiguration appConfig, IHttpContextAccessor contextAccessor)
    {
        _permissionSets = appConfig.PermissionSets;
        _contextAccessor = contextAccessor;
    }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;
        var permissionLevel = _contextAccessor?.HttpContext?.User.Claims
            .FirstOrDefault(claim => claim.Type == ClaimTypes.Role)?.Value ?? EFClient.Permission.User.ToString();

        var hasPermission = permissionLevel != null && _permissionSets.ContainsKey(permissionLevel) &&
                            _permissionSets[permissionLevel].HasPermission(Entity, Permission);
        if (!hasPermission)
        {
            output.SuppressOutput();
        }
    }
}
