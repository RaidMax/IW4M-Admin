using System;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models.Client;
using Data.Models.Misc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.Commands
{
    public class OfflineMessageCommand : Command
    {
        private readonly IDatabaseContextFactory _contextFactory;
        private readonly ILogger _logger;
        private const short MaxLength = 1024;
        
        public OfflineMessageCommand(CommandConfiguration config, ITranslationLookup layout,
            IDatabaseContextFactory contextFactory, ILogger<IDatabaseContextFactory> logger) : base(config, layout)
        {
            Name = "offlinemessage";
            Description = _translationLookup["COMMANDS_OFFLINE_MESSAGE_DESC"];
            Alias = "om";
            Permission = EFClient.Permission.Moderator;
            RequiresTarget = true;
            
            _contextFactory = contextFactory;
            _logger = logger;
        }

        public override async Task ExecuteAsync(GameEvent gameEvent)
        {
            if (gameEvent.Data.Length > MaxLength)
            {
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_OFFLINE_MESSAGE_TOO_LONG"].FormatExt(MaxLength));
                return;
            }
            
            if (gameEvent.Target.ClientId == gameEvent.Origin.ClientId)
            {
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_OFFLINE_MESSAGE_SELF"].FormatExt(MaxLength));
                return;
            }
            
            if (gameEvent.Target.IsIngame)
            {
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_OFFLINE_MESSAGE_INGAME"].FormatExt(gameEvent.Target.Name));
                return;
            }
            
            await using var context = _contextFactory.CreateContext(enableTracking: false);
            var server = await context.Servers.FirstAsync(srv => srv.EndPoint == gameEvent.Owner.ToString());

            var newMessage = new EFInboxMessage()
            {
                SourceClientId = gameEvent.Origin.ClientId,
                DestinationClientId = gameEvent.Target.ClientId,
                ServerId = server.Id,
                Message = gameEvent.Data,
            };

            try
            {
                context.Set<EFInboxMessage>().Add(newMessage);
                await context.SaveChangesAsync();
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_OFFLINE_MESSAGE_SUCCESS"]);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not save offline message {@Message}", newMessage);
                throw;
            }
        }
    }
}