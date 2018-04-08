using System;
using System.Collections.Generic;

using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Objects;

namespace IW4MAdmin.Application.API
{
    class EventApi : IEventApi
    {
        Queue<EventInfo> Events = new Queue<EventInfo>();
        DateTime LastFlagEvent;
        static string[] FlaggedMessageContains =
        {
            " wh ",
            "hax",
            "cheat",
            " hack ",
            "aim",
            "wall",
            "cheto",
            "hak",
            "bot"
        };
        int FlaggedMessageCount;

        public Queue<EventInfo> GetEvents() => Events;

        public void OnServerEvent(object sender, Event E)
        {
            if (E.Type == Event.GType.Say && E.Origin.Level < Player.Permission.Trusted)
            {
                bool flaggedMessage = false;
                foreach (string msg in FlaggedMessageContains)
                    flaggedMessage = flaggedMessage ? flaggedMessage : E.Data.ToLower().Contains(msg);

                if (flaggedMessage)
                    FlaggedMessageCount++;

                if (FlaggedMessageCount > 3)
                {
                    if (Events.Count > 20)
                        Events.Dequeue();

                    FlaggedMessageCount = 0;

                    E.Owner.Broadcast("If you suspect someone of ^5CHEATING ^7use the ^5!report ^7command").Wait();
                    Events.Enqueue(new EventInfo(
                        EventInfo.EventType.ALERT,
                        EventInfo.EventVersion.IW4MAdmin,
                        "Chat indicates there may be a cheater",
                        "Alert",
                        E.Owner.Hostname, ""));
                }

                if ((DateTime.UtcNow - LastFlagEvent).Minutes >= 3)
                {
                    FlaggedMessageCount = 0;
                    LastFlagEvent = DateTime.Now;
                }
            }

            if (E.Type == Event.GType.Report)
            {
                Events.Enqueue(new EventInfo(
                    EventInfo.EventType.ALERT,
                    EventInfo.EventVersion.IW4MAdmin,
                    $"**{E.Origin.Name}** has reported **{E.Target.Name}** for: {E.Data.Trim()}",
                    E.Target.Name, E.Origin.Name, ""));
            }
        }
    }
}
