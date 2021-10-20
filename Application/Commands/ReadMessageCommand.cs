using System;
using System.Linq;
using System.Threading.Tasks;
using Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using EFClient = Data.Models.Client.EFClient;
using ILogger = Microsoft.Extensions.Logging.ILogger;


namespace IW4MAdmin.Application.Commands
{
    public class ReadMessageCommand : Command
    {
        private readonly IDatabaseContextFactory _contextFactory;
        private readonly ILogger _logger;

        public ReadMessageCommand(CommandConfiguration config, ITranslationLookup layout,
            IDatabaseContextFactory contextFactory, ILogger<IDatabaseContextFactory> logger) : base(config, layout)
        {
            Name = "readmessage";
            Description = _translationLookup["COMMANDS_READ_MESSAGE_DESC"];
            Alias = "rm";
            Permission = EFClient.Permission.Flagged;

            _contextFactory = contextFactory;
            _logger = logger;
        }

        public override async Task ExecuteAsync(GameEvent gameEvent)
        {
            try
            {
                await using var context = _contextFactory.CreateContext();

                var inboxItems = await context.InboxMessages
                    .Include(message => message.SourceClient)
                    .ThenInclude(client => client.CurrentAlias)
                    .Where(message => message.DestinationClientId == gameEvent.Origin.ClientId)
                    .Where(message => !message.IsDelivered)
                    .ToListAsync();

                if (!inboxItems.Any())
                {
                    gameEvent.Origin.Tell(_translationLookup["COMMANDS_READ_MESSAGE_NONE"]);
                    return;
                }

                var index = 1;
                foreach (var inboxItem in inboxItems)
                {
                    await gameEvent.Origin.Tell(_translationLookup["COMMANDS_READ_MESSAGE_SUCCESS"]
                            .FormatExt($"{index}/{inboxItems.Count}", inboxItem.SourceClient.CurrentAlias.Name))
                        .WaitAsync();

                    foreach (var messageFragment in inboxItem.Message.FragmentMessageForDisplay())
                    {
                        await gameEvent.Origin.Tell(messageFragment).WaitAsync();
                    }

                    index++;
                }

                inboxItems.ForEach(item => { item.IsDelivered = true; });

                context.UpdateRange(inboxItems);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not retrieve offline messages for {Client}", gameEvent.Origin.ToString());
                throw;
            }
        }
    }
}