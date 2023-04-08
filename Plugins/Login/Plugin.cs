using System.Threading;
using System.Threading.Tasks;
using IW4MAdmin.Plugins.Login.Commands;
using Microsoft.Extensions.DependencyInjection;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;
using EFClient = Data.Models.Client.EFClient;

namespace IW4MAdmin.Plugins.Login;

public class Plugin : IPluginV2
{
    public string Name => "Login";
    public string Version => Utilities.GetVersionAsString();
    public string Author => "RaidMax";
    
    private readonly LoginStates _loginStates;

    public Plugin(LoginConfiguration configuration, LoginStates loginStates)
    {
        _loginStates = loginStates;
        if (!(configuration?.RequirePrivilegedClientLogin ?? false))
        {
            return;
        }

        IManagementEventSubscriptions.Load += OnLoad;
        IManagementEventSubscriptions.ClientStateInitialized += OnClientStateInitialized;
        IManagementEventSubscriptions.ClientStateDisposed += (clientEvent, token) =>
        {
            _loginStates.AuthorizedClients.TryRemove(clientEvent.Client.ClientId, out _);
            return Task.CompletedTask;
        };
    }

    public static void RegisterDependencies(IServiceCollection serviceCollection)
    {
        serviceCollection.AddConfiguration<LoginConfiguration>("LoginPluginSettings");
        serviceCollection.AddSingleton(new LoginStates());
    }

    private Task OnClientStateInitialized(ClientStateInitializeEvent clientEvent, CancellationToken token)
    {
        _loginStates.AuthorizedClients.TryAdd(clientEvent.Client.ClientId, false);
        clientEvent.Client.SetAdditionalProperty(LoginStates.LoginKey, false);

        return Task.CompletedTask;
    }

    private Task OnLoad(IManager manager, CancellationToken token)
    {
        manager.CommandInterceptors.Add(gameEvent =>
        {
            if (gameEvent.Type != GameEvent.EventType.Command || gameEvent.Extra is null || gameEvent.IsRemote)
            {
                return true;
            }

            if (gameEvent.Origin.Level is < EFClient.Permission.Moderator or EFClient.Permission.Console)
            {
                return true;
            }

            if (gameEvent.Extra.GetType() == typeof(SetPasswordCommand) &&
                gameEvent.Origin?.Password == null)
            {
                return true;
            }

            if (gameEvent.Extra.GetType() == typeof(LoginCommand))
            {
                return true;
            }

            if (gameEvent.Extra.GetType() == typeof(RequestTokenCommand))
            {
                return true;
            }

            if (!_loginStates.AuthorizedClients[gameEvent.Origin.ClientId])
            {
                return false;
            }

            gameEvent.Origin.SetAdditionalProperty(LoginStates.LoginKey, true);
            return true;
        });

        return Task.CompletedTask;
    }
}
