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

    public void SetLocalization(System.Collections.Generic.Dictionary<string, string> localization)
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

    private void NotifyStateChanged() => OnChange?.Invoke();
}
