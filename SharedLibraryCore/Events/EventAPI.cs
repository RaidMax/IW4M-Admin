﻿using System;
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

        public static void OnGameEvent(object sender, GameEventArgs eventState)
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
