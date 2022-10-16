using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Services;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.Misc;

public class RemoteCommandService : IRemoteCommandService
{
    private readonly ILogger _logger;
    private readonly ApplicationConfiguration _appConfig;
    private readonly ClientService _clientService;

    public RemoteCommandService(ILogger<RemoteCommandService> logger, ApplicationConfiguration appConfig, ClientService clientService)
    {
        _logger = logger;
        _appConfig = appConfig;
        _clientService = clientService;
    }

    public async Task<IEnumerable<CommandResponseInfo>> Execute(int originId, int? targetId, string command,
        IEnumerable<string> arguments, Server server)
    {
        if (originId < 1)
        {
            _logger.LogWarning("Not executing command {Command} for {Originid} because origin id is invalid", command,
                originId);
            return Enumerable.Empty<CommandResponseInfo>();
        }
        
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
