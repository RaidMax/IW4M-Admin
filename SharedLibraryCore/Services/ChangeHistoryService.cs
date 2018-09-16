using SharedLibraryCore.Database;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Events;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
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
