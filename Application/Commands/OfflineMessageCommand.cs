using System;
using System.Linq;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models.Client;
using Data.Models.Misc;
using IW4MAdmin.Application.Alerts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Alerts;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.Commands
{
    public class OfflineMessageCommand : Command
    {
        private readonly IDatabaseContextFactory _contextFactory;
        private readonly ILogger _logger;
        private readonly IAlertManager _alertManager;
        private const short MaxLength = 1024;

        public OfflineMessageCommand(CommandConfiguration config, ITranslationLookup layout,
            IDatabaseContextFactory contextFactory, ILogger<IDatabaseContextFactory> logger, IAlertManager alertManager)
            : base(config, layout)
        {
            Name = "offlinemessage";
            Description = _translationLookup["COMMANDS_OFFLINE_MESSAGE_DESC"];
            Alias = "om";
            Permission = EFClient.Permission.Moderator;
            RequiresTarget = true;

            _contextFactory = contextFactory;
            _logger = logger;
            _alertManager = alertManager;

            _alertManager.RegisterStaticAlertSource(async () =>
            {
                var context = contextFactory.CreateContext(false);
                return await context.InboxMessages.Where(message => !message.IsDelivered)
                    .Where(message => message.CreatedDateTime >= DateTime.UtcNow.AddDays(-7))
                    .Where(message => message.DestinationClient.Level > EFClient.Permission.User)
                    .Select(message => new Alert.AlertState
                    {
                        OccuredAt = message.CreatedDateTime,
                        Message = message.Message,
                        ExpiresAt = DateTime.UtcNow.AddDays(7),
                        Category = Alert.AlertCategory.Message,
                        Source = message.SourceClient.CurrentAlias.Name.StripColors(),
                        SourceId = message.SourceClientId,
                        RecipientId = message.DestinationClientId,
                        ReferenceId = message.InboxMessageId,
                        Type = nameof(EFInboxMessage)
                    }).ToListAsync();
            });

            _alertManager.OnAlertConsumed += (_, state) =>
            {
                if (state.Category != Alert.AlertCategory.Message || state.ReferenceId is null)
                {
                    return;
                }

                try
                {
                    var context = contextFactory.CreateContext(true);
                    foreach (var message in context.InboxMessages
                                 .Where(message => message.InboxMessageId == state.ReferenceId.Value).ToList())
                    {
                        message.IsDelivered = true;
                    }

                    context.SaveChanges();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not update message state for alert {@Alert}", state);
                }
            };
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
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_OFFLINE_MESSAGE_INGAME"]
                    .FormatExt(gameEvent.Target.Name));
                return;
            }

            await using var context = _contextFactory.CreateContext(enableTracking: false);
            var server = await context.Servers.FirstAsync(srv => srv.EndPoint == gameEvent.Owner.ToString());

            var newMessage = new EFInboxMessage
            {
                SourceClientId = gameEvent.Origin.ClientId,
                DestinationClientId = gameEvent.Target.ClientId,
                ServerId = server.Id,
                Message = gameEvent.Data,
            };

            _alertManager.AddAlert(gameEvent.Target.BuildAlert(Alert.AlertCategory.Message)
                .WithMessage(gameEvent.Data.Trim())
                .FromClient(gameEvent.Origin)
                .OfType(nameof(EFInboxMessage))
                .ExpiresIn(TimeSpan.FromDays(7)));

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
