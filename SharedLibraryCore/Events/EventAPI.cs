using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SharedLibraryCore.Database;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;

namespace SharedLibraryCore.Events
{
    public class EventApi
    {
        const int MaxEvents = 100;
        static Queue<EventInfo> RecentEvents = new Queue<EventInfo>();

        public static IEnumerable<EventInfo> GetEvents(bool shouldConsume)
        {
            var eventList = RecentEvents.ToArray();

            // clear queue if events should be consumed
            if (shouldConsume)
            {
                RecentEvents.Clear();
            }

            return eventList;
        }

        private static async Task SaveChangeHistory(GameEvent e)
        {
            EFChangeHistory change = null;

            switch (e.Type)
            {
                case GameEvent.EventType.Unknown:
                    break;
                case GameEvent.EventType.Start:
                    break;
                case GameEvent.EventType.Stop:
                    break;
                case GameEvent.EventType.Connect:
                    break;
                case GameEvent.EventType.Join:
                    break;
                case GameEvent.EventType.Quit:
                    break;
                case GameEvent.EventType.Disconnect:
                    break;
                case GameEvent.EventType.MapEnd:
                    break;
                case GameEvent.EventType.MapChange:
                    break;
                case GameEvent.EventType.Say:
                    break;
                case GameEvent.EventType.Warn:
                    break;
                case GameEvent.EventType.Report:
                    break;
                case GameEvent.EventType.Flag:
                    break;
                case GameEvent.EventType.Unflag:
                    break;
                case GameEvent.EventType.Kick:
                    break;
                case GameEvent.EventType.TempBan:
                    break;
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
                case GameEvent.EventType.Broadcast:
                    break;
                case GameEvent.EventType.Tell:
                    break;
                case GameEvent.EventType.ScriptDamage:
                    break;
                case GameEvent.EventType.ScriptKill:
                    break;
                case GameEvent.EventType.Damage:
                    break;
                case GameEvent.EventType.Kill:
                    break;
                case GameEvent.EventType.JoinTeam:
                    break;
            }

            if (change != null)
            {
                using (var ctx = new DatabaseContext(true))
                {
                    ctx.EFChangeHistory.Add(change);
                    await ctx.SaveChangesAsync();
                }
            }
        }

        public static async void OnGameEvent(object sender, GameEventArgs eventState)
        {
            var E = eventState.Event;
            // don't want to clog up the api with unknown events
            if (E.Type == GameEvent.EventType.Unknown)
                return;

            var apiEvent = new EventInfo()
            {
                ExtraInfo = E.Extra?.ToString() ?? E.Data,
                GameInfo = new EntityInfo()
                {
                    Name = E.Owner.GameName.ToString(),
                    Id = (int)E.Owner.GameName
                },
                OwnerEntity = new EntityInfo()
                {
                    Name = E.Owner.Hostname,
                    Id = E.Owner.GetHashCode()
                },
                OriginEntity = E.Origin == null ? null : new EntityInfo()
                {
                    Id = E.Origin.ClientId,
                    Name = E.Origin.Name
                },
                TargetEntity = E.Target == null ? null : new EntityInfo()
                {
                    Id = E.Target.ClientId,
                    Name = E.Target.Name
                },
                EventType = new EntityInfo()
                {
                    Id = (int)E.Type,
                    Name = E.Type.ToString()
                },
                EventTime = E.Time
            };

            // add the new event to the list
            AddNewEvent(apiEvent);

            await SaveChangeHistory(E);
        }

        /// <summary>
        /// Adds event to the list and removes first added if reached max capacity
        /// </summary>
        /// <param name="info">EventInfo to add</param>
        private static void AddNewEvent(EventInfo info)
        {
            // remove the first added event
            if (RecentEvents.Count >= MaxEvents)
                RecentEvents.Dequeue();

            RecentEvents.Enqueue(info);
        }
    }
}
