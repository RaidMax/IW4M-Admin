using System;
using System.Collections.Generic;
using System.Text;
using SharedLibrary;
using SharedLibrary.Interfaces;
using System.Threading.Tasks;

namespace EventAPI
{
    class EventsJSON : IPage
    {
        private struct EventResponse
        {
            public int eventCount;
            public RestEvent Event;
        }

        public string GetName()
        {
            return "Events";
        }

        public string GetPath()
        {
            return "/api/events";
        }

        public HttpResponse GetPage(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            bool shouldQuery = querySet.Get("status") != null;
            EventResponse requestedEvent = new EventResponse();
            HttpResponse resp = new HttpResponse();

            if (shouldQuery)
            {
                StringBuilder s = new StringBuilder();
                foreach (var S in Events.ActiveServers)
                    s.Append(String.Format("{0} has {1}/{4} players playing {2} on {3}\n", S.Hostname, S.GetPlayersAsList().Count, Utilities.GetLocalizedGametype(S.Gametype), S.CurrentMap.Name, S.MaxClients));
                requestedEvent.Event = new RestEvent(RestEvent.EventType.STATUS, RestEvent.EventVersion.IW4MAdmin, s.ToString(), "Status", "", "");
                requestedEvent.eventCount = 1; 
            }

            else if (Events.APIEvents.Count > 0)
            {
                requestedEvent.Event = Events.APIEvents.Dequeue();
                requestedEvent.eventCount = 1;       
            }

            else
            {
                requestedEvent.eventCount = 0;
            }

            resp.content = Newtonsoft.Json.JsonConvert.SerializeObject(requestedEvent);
            resp.contentType = GetContentType();           
            resp.additionalHeaders = new Dictionary<string, string>();
            return resp;
        }

        public string GetContentType()
        {
            return "application/json";
        }

        public bool Visible()
        {
            return false;
        }
    }

    class Events : IPlugin
    {
        public static Queue<RestEvent> APIEvents { get; private set; }
        public static List<Server> ActiveServers;

        DateTime lastClear;
        int flaggedMessages;
        List<string> flaggedMessagesText;

        public String Name
        {
            get { return "Event API Plugin"; }
        }

        public float Version
        {
            get { return 1.0f; }
        }

        public string Author
        {
            get
            {
                return "RaidMax";
            }
        }

        public async Task OnLoadAsync(IManager manager)
        {
            APIEvents = new Queue<RestEvent>();
            flaggedMessagesText = new List<string>();
            ActiveServers = new List<Server>();
            WebService.PageList.Add(new EventsJSON());
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
                // fixme: this will be bad once FTP is working and there can be multiple servers on the same port.
                ActiveServers.RemoveAll(s => s.GetPort() == S.GetPort());
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
                    message.Contains("bot");

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
