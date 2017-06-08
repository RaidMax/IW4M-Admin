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

        public string getName()
        {
            return "Events";
        }

        public string getPath()
        {
            return "/api/events";
        }

        public HttpResponse getPage(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            bool shouldQuery = querySet.Get("status") != null;
            EventResponse requestedEvent = new EventResponse();
            HttpResponse resp = new HttpResponse();

            if (shouldQuery)
            {
                StringBuilder s = new StringBuilder();
                foreach (var S in Events.activeServers)
                    s.Append(String.Format("{0} has {1}/{4} players playing {2} on {3}\n", S.getName(), S.GetPlayersAsList().Count, Utilities.gametypeLocalized(S.getGametype()), S.CurrentMap.Name, S.MaxClients));
                requestedEvent.Event = new RestEvent(RestEvent.eType.STATUS, RestEvent.eVersion.IW4MAdmin, s.ToString(), "Status", "", "");
                requestedEvent.eventCount = 1; 
            }

            else if (Events.apiEvents.Count > 0)
            {
                requestedEvent.Event = Events.apiEvents.Dequeue();
                requestedEvent.eventCount = 1;       
            }

            else
            {
                requestedEvent.eventCount = 0;
            }

            resp.content = Newtonsoft.Json.JsonConvert.SerializeObject(requestedEvent);
            resp.contentType = getContentType();           
            resp.additionalHeaders = new Dictionary<string, string>();
            return resp;
        }

        public string getContentType()
        {
            return "application/json";
        }

        public bool isVisible()
        {
            return false;
        }
    }

    class Events : IPlugin
    {
        public static Queue<RestEvent> apiEvents { get; private set; }
        public static List<Server> activeServers;

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

        public async Task OnLoadAsync()
        {
            apiEvents = new Queue<RestEvent>();
            flaggedMessagesText = new List<string>();
            activeServers = new List<Server>();
            WebService.pageList.Add(new EventsJSON());
        }

        public async Task OnUnloadAsync()
        {
            apiEvents.Clear();
            activeServers.Clear();
        }

        public async Task OnTickAsync(Server S)
        {
            return;
        }

        public async Task OnEventAsync(Event E, Server S)
        {
            if (E.Type == Event.GType.Start)
            {
                activeServers.Add(S);
            }

            if (E.Type == Event.GType.Stop)
            {
                // fixme: this will be bad once FTP is working and there can be multiple servers on the same port.
                activeServers.RemoveAll(s => s.getPort() == S.getPort());
            }

            if (E.Type == Event.GType.Connect)
            {
                addRestEvent(new RestEvent(RestEvent.eType.NOTIFICATION, RestEvent.eVersion.IW4MAdmin, E.Origin.Name + " has joined " + S.getName(), E.Type.ToString(), S.getName(), E.Origin.Name));
            }

            if (E.Type == Event.GType.Disconnect)
            {
                addRestEvent(new RestEvent(RestEvent.eType.NOTIFICATION, RestEvent.eVersion.IW4MAdmin, E.Origin.Name + " has left " + S.getName(), E.Type.ToString(), S.getName(), E.Origin.Name));
            }

            if (E.Type == Event.GType.Say)
            {
                if (E.Data.Length != 0 && E.Data[0] != '!')
                    addRestEvent(new RestEvent(RestEvent.eType.NOTIFICATION, RestEvent.eVersion.IW4MAdmin, E.Data, "Chat", E.Origin.Name, ""));
            }

            if (E.Type == Event.GType.Say && E.Origin.Level < Player.Permission.Moderator)
            {
                string message = E.Data.ToLower();
                bool flagged = message.Contains(" wh ") || message.Contains("hax") || message.Contains("cheat") || message.Contains("hack") || message.Contains("aim") || message.Contains("wall") || message.Contains("cheto") || message.Contains("hak");

                if (flagged)
                {
                    flaggedMessages++;
                    flaggedMessagesText.Add(String.Format("{0}: {1}", E.Origin.Name, E.Data));
                }

                if (flaggedMessages > 3)
                {
                    await E.Owner.Broadcast("If you suspect someone of ^5CHEATING ^7use the ^5!report ^7command");

                    addRestEvent(new RestEvent(RestEvent.eType.ALERT, RestEvent.eVersion.IW4MAdmin, "Chat indicates there may be a cheater", "Alert", E.Owner.getName(), ""));
                    addRestEvent(new RestEvent(RestEvent.eType.NOTIFICATION, RestEvent.eVersion.IW4MAdmin, String.Join("\n", flaggedMessagesText), "Chat Monitor", E.Owner.getName(), ""));
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

        public static void addRestEvent(RestEvent E)
        {
            if (apiEvents.Count > 10)
                apiEvents.Dequeue();
            apiEvents.Enqueue(E);
        }
    }
}
