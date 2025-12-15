using Microsoft.AspNetCore.Components;
using WebfrontCore.Controllers.API;
using WebfrontCore.Services;

namespace WebfrontCore.Components.Pages.About;

public partial class Index
{
    [Inject] public required IWebfrontApiClient Api { get; set; }
    [Inject] public required AppState AppState { get; set; }
    private AboutDto AboutInfo { get; set; }
    private List<RuleSetInfo> AllRules { get; set; } = [];

    protected override async Task OnInitializedAsync()
    {
        AboutInfo = await Api.GetAboutAsync();

        if (AboutInfo != null)
        {
            // Build all rules list similar to MVC
            AllRules = [];

            // Global rules first
            if (AboutInfo.GlobalRules?.Any() == true)
            {
                AllRules.Add(new RuleSetInfo
                {
                    ServerName = AppState.Loc("WEBFRONT_ABOUT_GLOBAL_RULES"),
                    Rules = AboutInfo.GlobalRules.ToList()
                });
            }

            // Server-specific rules
            if (AboutInfo.ServerRules != null)
            {
                foreach (var server in AboutInfo.ServerRules.Where(s => s.Rules?.Any() == true))
                {
                    AllRules.Add(new RuleSetInfo
                    {
                        ServerName = server.ServerName,
                        Rules = server.Rules.ToList()
                    });
                }
            }
        }
    }

    private class RuleSetInfo
    {
        public string ServerName { get; set; }
        public List<string> Rules { get; set; } = [];
    }
}
