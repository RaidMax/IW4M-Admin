using SharedLibrary;
using System;
using System.Collections.Generic;

namespace WebfrontCore.Application.API
{
    class EventAPI
    {
        public static Queue<SharedLibrary.Dtos.EventInfo> Events = new Queue<SharedLibrary.Dtos.EventInfo>();
        static DateTime LastFlagEvent;
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
        static int FlaggedMessageCount;

        public static void OnServerEventOccurred(object sender, Event E)
        {
            if (E.Type == Event.GType.Say && E.Origin.Level < SharedLibrary.Objects.Player.Permission.Trusted)
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
                    Events.Enqueue(new SharedLibrary.Dtos.EventInfo(
                        SharedLibrary.Dtos.EventInfo.EventType.ALERT,
                        SharedLibrary.Dtos.EventInfo.EventVersion.IW4MAdmin,
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
                Events.Enqueue(new SharedLibrary.Dtos.EventInfo(
                    SharedLibrary.Dtos.EventInfo.EventType.ALERT,
                    SharedLibrary.Dtos.EventInfo.EventVersion.IW4MAdmin,
                    $"**{E.Origin.Name}** has reported **{E.Target.Name}** for: {E.Data.Trim()}",
                    E.Target.Name, E.Origin.Name, ""));
            }
        }
    }
}
