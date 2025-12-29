using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Configuration;
using WebfrontCore.Components.Features.Home.Models;
using WebfrontCore.Core.Services;

namespace WebfrontCore.Components.Features.Home.Pages;

public partial class About
{
    [Inject] public required IWebfrontDataService DataService { get; set; }
    [Inject] public required AppState AppState { get; set; }
    [Inject] public required ApplicationConfiguration AppConfig { get; set; }
    [Inject] public required NavigationManager NavManager { get; set; }

    [PersistentState] public AboutInfo? AboutInfo { get; set; }
    private List<RuleSetInfo> AllRules { get; set; } = [];

    protected override async Task OnInitializedAsync()
    {
        AboutInfo = await DataService.GetAboutInfoAsync();

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
