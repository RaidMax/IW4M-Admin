﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models;
using Data.Models.Client;
using Microsoft.Extensions.Logging;

namespace SharedLibraryCore.Services
{
    public class ChangeHistoryService
    {
        private readonly IDatabaseContextFactory _contextFactory;
        private readonly ILogger _logger;

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
                    change = new EFChangeHistory
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
                            e.Message = string.Join(' ',
                                e.Message.Split(" ").Select((arg, index) => index > 0 ? "*****" : arg));
                        }
                    }

                    change = new EFChangeHistory
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
                    change = new EFChangeHistory
                    {
                        OriginEntityId = e.Origin.ClientId,
                        TargetEntityId = e.Target.ClientId,
                        ImpersonationEntityId = e.ImpersonationOrigin?.ClientId,
                        Comment = "Changed permission level",
                        TypeOfChange = EFChangeHistory.ChangeType.Permission,
                        CurrentValue = ((EFClient.Permission)e.Extra).ToString()
                    };
                    break;
                case GameEvent.EventType.Login:
                    change = new EFChangeHistory
                    {
                        OriginEntityId = e.Origin.ClientId,
                        Comment = "Logged In To Webfront",
                        TypeOfChange = EFChangeHistory.ChangeType.Command,
                        CurrentValue = e.Data
                    };
                    break;
                case GameEvent.EventType.Logout:
                    change = new EFChangeHistory
                    {
                        OriginEntityId = e.Origin.ClientId,
                        Comment = "Logged Out of Webfront",
                        TypeOfChange = EFChangeHistory.ChangeType.Command,
                        CurrentValue = e.Data
                    };
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