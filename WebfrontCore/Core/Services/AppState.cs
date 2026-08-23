using Data.Models;
using SharedLibraryCore.Dtos;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;

namespace WebfrontCore.Core.Services;

public class AppState(ApplicationConfiguration appConfig)
{
    public event Action OnChange = delegate { };

    public string WebfrontBranding => !string.IsNullOrEmpty(appConfig.Webfront.CustomBranding)
        ? appConfig.Webfront.CustomBranding
        : "IW4MAdmin";
    
    public WebfrontConfiguration WebfrontConfig => appConfig.Webfront;

    public bool SidebarCollapsed
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            NotifyStateChanged();
        }
    } = false;

    public bool IsMobileNavOpen
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            NotifyStateChanged();
        }
    }

    public bool IsAdvancedSearchOpen
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            NotifyStateChanged();
        }
    }

    public ClientInfo? User { get; private set; }

    /// <summary>
    /// Sets the user without triggering state change notifications.
    /// Use during component initialization to avoid render tree exceptions.
    /// </summary>
    public void InitializeUser(ClientInfo user)
    {
        User ??= user;
    }

    public void SetUser(ClientInfo user)
    {
        if (User?.ClientId == user.ClientId && 
            User?.PendingTwoFactorEnrollment == user.PendingTwoFactorEnrollment && 
            User?.HasTwoFactor == user.HasTwoFactor)
        {
            return;
        }
        
        User = user;
        NotifyStateChanged();
    }

    public string Loc(string key)
    {
        if (key == "GAME_D7D")
        {
            return "7 Days to Die";
        }

        try
        {
            return Utilities.CurrentLocalization?.LocalizationIndex?[key] ?? key;
        }
        catch
        {
            return key;
        }
    }

    public static string GetLevelColorClass(Data.Models.Client.EFClient.Permission permission) => permission switch
    {
        Data.Models.Client.EFClient.Permission.Console => "text-level-console",
        Data.Models.Client.EFClient.Permission.Owner => "text-level-owner",
        Data.Models.Client.EFClient.Permission.Creator => "text-level-owner",
        Data.Models.Client.EFClient.Permission.SeniorAdmin => "text-level-senioradmin",
        Data.Models.Client.EFClient.Permission.Administrator => "text-level-administrator",
        Data.Models.Client.EFClient.Permission.Moderator => "text-level-moderator",
        Data.Models.Client.EFClient.Permission.Trusted => "text-level-trusted",
        Data.Models.Client.EFClient.Permission.Flagged => "text-level-flagged",
        Data.Models.Client.EFClient.Permission.Banned => "text-red-500 font-bold",
        _ => "text-slate-400"
    };

    public static string GetPenaltyBadgeClass(EFPenalty.PenaltyType type) => type switch
    {
        EFPenalty.PenaltyType.Ban => "bg-red-900/30 text-red-400 border-red-900/50",
        EFPenalty.PenaltyType.TempBan => "bg-red-900/30 text-red-400 border-red-900/50",
        EFPenalty.PenaltyType.Kick => "bg-orange-900/30 text-orange-400 border-orange-900/50",
        EFPenalty.PenaltyType.Warning => "bg-yellow-900/30 text-yellow-400 border-yellow-900/50",
        EFPenalty.PenaltyType.Unban => "bg-emerald-900/30 text-emerald-400 border-emerald-900/50",
        _ => "bg-slate-700 text-slate-300 border-slate-600"
    };

    private void NotifyStateChanged() => OnChange?.Invoke();
}
