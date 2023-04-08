using System.Collections.Concurrent;

namespace IW4MAdmin.Plugins.Login;

public class LoginStates
{
    public ConcurrentDictionary<int, bool> AuthorizedClients { get; } = new();
    public const string LoginKey = "IsLoggedIn";
}
