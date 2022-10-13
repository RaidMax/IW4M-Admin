using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Services;

namespace IW4MAdmin.Application.Misc;

public class RemoteCommandService : IRemoteCommandService
{
    private readonly ApplicationConfiguration _appConfig;
    private readonly ClientService _clientService;

    public RemoteCommandService(ApplicationConfiguration appConfig, ClientService clientService)
    {
        _appConfig = appConfig;
        _clientService = clientService;
    }

    public async Task<IEnumerable<CommandResponseInfo>> Execute(int originId, int? targetId, string command,
        IEnumerable<string> arguments, Server server)
    {
        var client = await _clientService.Get(originId);
        client.CurrentServer = server;

        command += $" {(targetId.HasValue ? $"@{targetId} " : "")}{string.Join(" ", arguments ?? Enumerable.Empty<string>())}";

        var remoteEvent = new GameEvent
        {
            Type = GameEvent.EventType.Command,
            Data = command.StartsWith(_appConfig.CommandPrefix) ||
                   command.StartsWith(_appConfig.BroadcastCommandPrefix)
                ? command
                : $"{_appConfig.CommandPrefix}{command}",
            Origin = client,
            Owner = server,
            IsRemote = true
        };

        server.Manager.AddEvent(remoteEvent);
        CommandResponseInfo[] response;

        try
        {
            // wait for the event to process
            var completedEvent =
                await remoteEvent.WaitAsync(Utilities.DefaultCommandTimeout, server.Manager.CancellationToken);

            if (completedEvent.FailReason == GameEvent.EventFailReason.Timeout)
            {
                response = new[]
                {
                    new CommandResponseInfo()
                    {
                        ClientId = client.ClientId,
                        Response = Utilities.CurrentLocalization.LocalizationIndex["SERVER_ERROR_COMMAND_TIMEOUT"]
                    }
                };
            }

            else
            {
                response = completedEvent.Output.Select(output => new CommandResponseInfo()
                {
                    Response = output,
                    ClientId = client.ClientId
                }).ToArray();
            }
        }

        catch (System.OperationCanceledException)
        {
            response = new[]
            {
                new CommandResponseInfo
                {
                    ClientId = client.ClientId,
                    Response = Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_RESTART_SUCCESS"]
                }
            };
        }

        return response;
    }
}
