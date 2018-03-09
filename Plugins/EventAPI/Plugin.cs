using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using SharedLibrary;
using SharedLibrary.Interfaces;
using SharedLibrary.Objects;

namespace EventAPI
{
    class Events : IPlugin
    {
        public static Queue<RestEvent> APIEvents { get; private set; }
        public static List<Server> ActiveServers;

        DateTime lastClear;
        int flaggedMessages;
        List<string> flaggedMessagesText;

        public String Name => "Event API Plugin";

        public float Version => 1.0f;

        public string Author => "RaidMax";

        public async Task OnLoadAsync(IManager manager)
        {
            APIEvents = new Queue<RestEvent>();
            flaggedMessagesText = new List<string>();
            ActiveServers = new List<Server>();
        }

        public async Task OnUnloadAsync()
        {
            APIEvents.Clear();
            ActiveServers.Clear();
        }

        public async Task OnTickAsync(Server S)
        {
            return;
        }

        public async Task OnEventAsync(Event E, Server S)
        {
            if (E.Type == Event.GType.Start)
            {
                ActiveServers.Add(S);
            }

            if (E.Type == Event.GType.Stop)
            {
                ActiveServers.RemoveAll(s => s.GetHashCode() == S.GetHashCode());
            }

            if (E.Type == Event.GType.Connect)
            {
                AddRestEvent(new RestEvent(RestEvent.EventType.NOTIFICATION, RestEvent.EventVersion.IW4MAdmin, E.Origin.Name + " has joined " + S.Hostname, E.Type.ToString(), S.Hostname, E.Origin.Name));
            }

            if (E.Type == Event.GType.Disconnect)
            {
                AddRestEvent(new RestEvent(RestEvent.EventType.NOTIFICATION, RestEvent.EventVersion.IW4MAdmin, E.Origin.Name + " has left " + S.Hostname, E.Type.ToString(), S.Hostname, E.Origin.Name));
            }

            if (E.Type == Event.GType.Say)
            {
                if (E.Data.Length != 0 && E.Data[0] != '!')
                    AddRestEvent(new RestEvent(RestEvent.EventType.NOTIFICATION, RestEvent.EventVersion.IW4MAdmin, E.Data, "Chat", E.Origin.Name, ""));
            }

            if (E.Type == Event.GType.Report)
            {
                AddRestEvent(new RestEvent(RestEvent.EventType.ALERT, RestEvent.EventVersion.IW4MAdmin, $"**{E.Origin.Name}** has reported **{E.Target.Name}** for: {E.Data.Trim()}", E.Target.Name, E.Origin.Name, ""));
            }

            if (E.Type == Event.GType.Say && E.Origin.Level < Player.Permission.Moderator)
            {
                string message = E.Data.ToLower();
                bool flagged = message.Contains(" wh ") ||
                    message.Contains("hax") ||
                    message.Contains("cheat") ||
                    message.Contains(" hack ") ||
                    message.Contains("aim") ||
                    message.Contains("wall") ||
                    message.Contains("cheto") ||
                    message.Contains("hak") ||
                    message.Contains(" bot ");

                if (flagged)
                {
                    flaggedMessages++;
                    flaggedMessagesText.Add(String.Format("{0}: {1}", E.Origin.Name, E.Data));
                }

                if (flaggedMessages > 3)
                {
                    await E.Owner.Broadcast("If you suspect someone of ^5CHEATING ^7use the ^5!report ^7command");

                    AddRestEvent(new RestEvent(RestEvent.EventType.ALERT, RestEvent.EventVersion.IW4MAdmin, "Chat indicates there may be a cheater", "Alert", E.Owner.Hostname, ""));
                    AddRestEvent(new RestEvent(RestEvent.EventType.NOTIFICATION, RestEvent.EventVersion.IW4MAdmin, String.Join("\n", flaggedMessagesText), "Chat Monitor", E.Owner.Hostname, ""));
                    flaggedMessages = 0;
                }

                else if ((DateTime.Now - lastClear).TotalMinutes >= 3)
                {
                    flaggedMessages = 0;
                    flaggedMessagesText.Clear();
                    lastClear = DateTime.Now;
                }
            }
        }

        public static void AddRestEvent(RestEvent E)
        {
            if (APIEvents.Count > 20)
                APIEvents.Dequeue();
            APIEvents.Enqueue(E);
        }
    }
}
