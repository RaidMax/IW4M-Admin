using static Data.Models.Client.EFClient;

namespace SharedLibraryCore.Configuration;

public class WebfrontConfiguration
{
    public bool Enabled { get; set; }
    public string BindUrl { get; set; } = "http://0.0.0.0:1624";
    public string ManualUrl { get; set; }
    public string CustomBranding { get; set; }
    public bool EnableConnectionWhitelist { get; set; }
    public string[] ConnectionWhitelist { get; set; } = [];
    public string PrimaryColor { get; set; } = "#117ac0";
    public string SecondaryColor { get; set; } = "pink";
    public string ThemePreset { get; set; } = "minimal";
    public bool PreventUserCustomization { get; set; }
    
    public Dictionary<string, List<string>> PermissionSets { get; set; } = new()
    {
        {
            nameof(Permission.User), [
                "HelpPage.Read",
                "ProfilePage.Read",
                "Interaction.Read"
            ]
        },
        {
            nameof(Permission.Trusted), [
                "HelpPage.Read",
                "ProfilePage.Read",
                "Penalty.Read",
                "Interaction.Read",
                "ClientLevel.Read",
                "ConsolePage.Read",
                "PrivilegedClientsPage.Read"
            ]
        },
        {
            nameof(Permission.Moderator), [
                "HelpPage.Read",
                "ProfilePage.Read",
                "Penalty.Read",
                "Interaction.Read",
                "ClientLevel.Read",
                "PrivilegedClientsPage.Read",
                "AdminMenu.Read",
                "RecentPlayersPage.Read",
                "ClientNote.Read",
                "ConsolePage.Read",
                "AdvancedSearch.Read"
            ]
        },
        {
            nameof(Permission.Administrator), [
                "HelpPage.Read",
                "ProfilePage.Read",
                "Penalty.Read",
                "Interaction.Read",
                "ClientLevel.Read",
                "PrivilegedClientsPage.Read",
                "AdminMenu.Read",
                "RecentPlayersPage.Read",
                "ClientNote.Read",
                "AdvancedSearch.Read",
                "MetaAliasUpdate.Read",
                "ClientGuid.Read",
                "ConsolePage.Read",
                "AuditPage.Read"
            ]
        },
        {
            nameof(Permission.SeniorAdmin), [
                "*"
            ]
        },
        {
            nameof(Permission.Owner), [
                "*"
            ]
        },
        {
            nameof(Permission.Console), [
                "*"
            ]
        }
    };
}
