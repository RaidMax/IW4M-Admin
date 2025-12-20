using Data.Models;
using SharedLibraryCore.Dtos;
using SharedLibraryCore;

namespace WebfrontCore.Core.Services;

public class AppState
{
    public event Action OnChange;

    private bool _isDarkMode;

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (_isDarkMode != value)
            {
                _isDarkMode = value;
                NotifyStateChanged();
            }
        }
    }

    private bool _sidebarCollapsed = false;

    public bool SidebarCollapsed
    {
        get => _sidebarCollapsed;
        set
        {
            if (_sidebarCollapsed != value)
            {
                _sidebarCollapsed = value;
                NotifyStateChanged();
            }
        }
    }

    private bool _isMobileNavOpen;

    public bool IsMobileNavOpen
    {
        get => _isMobileNavOpen;
        set
        {
            if (_isMobileNavOpen != value)
            {
                _isMobileNavOpen = value;
                NotifyStateChanged();
            }
        }
    }

    private string _activeServerId;

    public string ActiveServerId
    {
        get => _activeServerId;
        set
        {
            if (_activeServerId != value)
            {
                _activeServerId = value;
                NotifyStateChanged();
            }
        }
    }

    public ClientInfo? User { get; private set; }
    public System.Collections.Generic.Dictionary<string, string> Localization { get; private set; }

    public void SetUser(ClientInfo user)
    {
        User = user;
        NotifyStateChanged();
    }

    public void SetLocalization(Dictionary<string, string> localization)
    {
        Localization = localization;
        NotifyStateChanged();
    }

    public string Loc(string key)
    {
        // Try API-fetched localization first, then fall back to direct access
        if (Localization != null && Localization.TryGetValue(key, out var value))
        {
            return value;
        }

        // Fall back to directly accessing the server-side localization
        // This works because Blazor Server runs on the same process
        try
        {
            return Utilities.CurrentLocalization?.LocalizationIndex?[key] ?? key;
        }
        catch
        {
            return key;
        }
    }

    public string GetLevelColorClass(Data.Models.Client.EFClient.Permission permission) => permission switch
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

    public string GetPenaltyBadgeClass(EFPenalty.PenaltyType type) => type switch
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
