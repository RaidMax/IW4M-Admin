using SharedLibraryCore.Database;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace SharedLibraryCore.Services
{
    public class ChangeHistoryService
    {
        private readonly ILogger _logger;
        private readonly IDatabaseContextFactory _contextFactory;
        
        public ChangeHistoryService(ILogger<ChangeHistoryService> logger, IDatabaseContextFactory contextFactory)
        {
            _logger = logger;
            _contextFactory = contextFactory;
        }

        public async Task Add(GameEvent e)
        {
            EFChangeHistory change = null;

            switch (e.Type)
            {
                case GameEvent.EventType.Ban:
                    change = new EFChangeHistory()
                    {
                        OriginEntityId = e.Origin.ClientId,
                        TargetEntityId = e.Target.ClientId,
                        ImpersonationEntityId = e.ImpersonationOrigin?.ClientId,
                        TypeOfChange = EFChangeHistory.ChangeType.Ban,
                        Comment = e.Data
                    };
                    break;
                case GameEvent.EventType.Command:
                    // this prevents passwords/tokens being logged into the database in plain text
                    if (e.Extra is Command cmd)
                    {
                        if (cmd.Name == "login" || cmd.Name == "setpassword")
                        {
                            e.Message = string.Join(' ', e.Message.Split(" ").Select((arg, index) => index > 0 ? "*****" : arg));
                        }
                    }
                    change = new EFChangeHistory()
                    {
                        OriginEntityId = e.Origin.ClientId,
                        TargetEntityId = e.Target?.ClientId ?? 0,
                        ImpersonationEntityId = e.ImpersonationOrigin?.ClientId,
                        Comment = "Executed command",
                        CurrentValue = e.Message,
                        TypeOfChange = EFChangeHistory.ChangeType.Command
                    };
                    break;
                case GameEvent.EventType.ChangePermission:
                    change = new EFChangeHistory()
                    {
                        OriginEntityId = e.Origin.ClientId,
                        TargetEntityId = e.Target.ClientId,
                        ImpersonationEntityId = e.ImpersonationOrigin?.ClientId,
                        Comment = "Changed permission level",
                        TypeOfChange = EFChangeHistory.ChangeType.Permission,
                        CurrentValue = ((EFClient.Permission)e.Extra).ToString()
                    };
                    break;
                default:
                    break;
            }

            if (change == null)
            {
                return;
            }

            await using var context = _contextFactory.CreateContext(false);

            context.EFChangeHistory.Add(change);

            try
            {
                await context.SaveChangesAsync();
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not persist change @{change}", change);
            }
        }
    }
}
