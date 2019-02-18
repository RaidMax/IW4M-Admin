using SharedLibraryCore.Database;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Events;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SharedLibraryCore.Services
{
    public class ChangeHistoryService : IEntityService<EFChangeHistory>
    {
        public Task<EFChangeHistory> Create(EFChangeHistory entity)
        {
            throw new NotImplementedException();
        }

        public async Task<EFChangeHistory> Add(GameEvent e)
        {
            EFChangeHistory change = null;

            switch (e.Type)
            {
                case GameEvent.EventType.Ban:
                    change = new EFChangeHistory()
                    {
                        OriginEntityId = e.Origin.ClientId,
                        TargetEntityId = e.Target.ClientId,
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
                        Comment = "Executed command",
                        PreviousValue = "",
                        CurrentValue = e.Message,
                        TypeOfChange = EFChangeHistory.ChangeType.Command
                    };
                    break;
                case GameEvent.EventType.ChangePermission:
                    change = new EFChangeHistory()
                    {
                        OriginEntityId = e.Origin.ClientId,
                        TargetEntityId = e.Target.ClientId,
                        TypeOfChange = EFChangeHistory.ChangeType.Permission,
                        PreviousValue = ((Change)e.Extra).PreviousValue,
                        CurrentValue = ((Change)e.Extra).NewValue
                    };
                    break;
                default:
                    break;
            }

            if (change != null)
            {
                using (var ctx = new DatabaseContext(true))
                {
                    ctx.EFChangeHistory.Add(change);
                    try
                    {
                        await ctx.SaveChangesAsync();
                    }

                    catch (Exception ex)
                    {
                        e.Owner.Logger.WriteWarning(ex.Message);
                        e.Owner.Logger.WriteDebug(ex.GetExceptionInfo());
                    }
                }
            }

            return change;
        }

        public Task<EFChangeHistory> Delete(EFChangeHistory entity)
        {
            throw new NotImplementedException();
        }

        public Task<IList<EFChangeHistory>> Find(Func<EFChangeHistory, bool> expression)
        {
            throw new NotImplementedException();
        }

        public Task<EFChangeHistory> Get(int entityID)
        {
            throw new NotImplementedException();
        }

        public Task<EFChangeHistory> GetUnique(long entityProperty)
        {
            throw new NotImplementedException();
        }

        public Task<EFChangeHistory> Update(EFChangeHistory entity)
        {
            throw new NotImplementedException();
        }
    }
}
