using System;
using System.Threading.Tasks;
using Data.Models.Client;
using Serilog.Core;
using Serilog.Events;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Commands;

public class SetLogLevelCommand : Command
{
    private readonly Func<string, LoggingLevelSwitch> _levelSwitchResolver;

    public SetLogLevelCommand(CommandConfiguration config, ITranslationLookup layout, Func<string, LoggingLevelSwitch> levelSwitchResolver) : base(config, layout)
    {
        _levelSwitchResolver = levelSwitchResolver;

        Name = "loglevel";
        Alias = "ll";
        Description = "set minimum logging level";
        Permission = EFClient.Permission.Owner;
        Arguments = new CommandArgument[]
        {
            new()
            {
                Name = "Log Level",
                Required = true
            },
            new()
            {
                Name = "Override",
                Required = false
            },
            new()
            {
                Name = "IsDevelopment",
                Required = false
            }
        };

    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        var args = gameEvent.Data.Split(" ");
        if (!Enum.TryParse<LogEventLevel>(args[0], out var minLevel))
        {
            await gameEvent.Origin.TellAsync(new[]
            {
                $"Valid log values: {string.Join(",", Enum.GetValues<LogEventLevel>())}"
            });
            return;
        }

        var context = string.Empty;
        
        if (args.Length > 1)
        {
            context = args[1];
        }

        var loggingSwitch = _levelSwitchResolver(context);
        loggingSwitch.MinimumLevel = minLevel;

        if (args.Length > 2 && (args[2] == "1" || args[2].ToLower() == "true"))
        {
            AppContext.SetSwitch("IsDevelop", true);
        }
        else
        {
            AppContext.SetSwitch("IsDevelop", false);
        }

        await gameEvent.Origin.TellAsync(new[]
            { $"Set minimum log level to {loggingSwitch.MinimumLevel.ToString()}" });
    }
}
