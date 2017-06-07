using Kayak;
using Kayak.Http;
using SharedLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace IW4MAdmin
{
    public class WebService
    {
        public static IServer webService;

        public static IScheduler GetScheduler()
        {
            var webScheduler = Kayak.KayakScheduler.Factory.Create(new Scheduler());
            webService = KayakServer.Factory.CreateHttp(new Request(), webScheduler);

            SharedLibrary.WebService.pageList.Add(new Pages());
            SharedLibrary.WebService.pageList.Add(new Homepage());
            SharedLibrary.WebService.pageList.Add(new ServersJSON());
            SharedLibrary.WebService.pageList.Add(new Penalties());
            SharedLibrary.WebService.pageList.Add(new PenaltiesJSON());
            SharedLibrary.WebService.pageList.Add(new Players());
            SharedLibrary.WebService.pageList.Add(new GetPlayer());
            SharedLibrary.WebService.pageList.Add(new WebConsole());
            SharedLibrary.WebService.pageList.Add(new ConsoleJSON());
            SharedLibrary.WebService.pageList.Add(new PubbansJSON());

            Thread scheduleThread = new Thread(() => { ScheduleThreadStart(webScheduler, webService); });
            scheduleThread.Name = "Web Service Thread";
            scheduleThread.Start();

            return webScheduler;
        }

        private static void ScheduleThreadStart(IScheduler S, IServer ss)
        {
            try
            {
                string[] webConfig = System.IO.File.ReadAllLines("config\\web.cfg");
                var address = Dns.GetHostAddresses(webConfig[0])[0];
                int port = Convert.ToInt32(webConfig[1]);

                try
                {
                    using (ss.Listen(new IPEndPoint(address, port)))
                        S.Start();
                }

                catch (Exception e)
                {
                    Manager.GetInstance().Logger.WriteError($"Unable to start webservice ( port is probably in use ): {e.Message}");
                    
                }
            }

            catch (Exception)
            {
                using (ss.Listen(new IPEndPoint(IPAddress.Any, 1624)))
                    S.Start();
            }
        }

        public static HttpResponse GetPage(string path, System.Collections.Specialized.NameValueCollection queryset, IDictionary<string, string> headers)
        {
            if (SharedLibrary.WebService.pageList == null || SharedLibrary.WebService.pageList.Count == 0)
                return new HttpResponse() { content = "Error: page list not initialized!", contentType = "text/plaintext" };

            if (path == null)
                return new HttpResponse() { content = "Error: no path specified", contentType = "text/plaintext" };

            IPage requestedPage = SharedLibrary.WebService.pageList.Find(x => x.getPath().ToLower() == path.ToLower());

            if (requestedPage != null)
                return requestedPage.getPage(queryset, headers);
            else
            {
                if (System.IO.File.Exists(path.Replace("/", "\\").Substring(1)))
                {
                    IFile f = new IFile(path.Replace("/", "\\").Substring(1));


                    if (path.Contains(".css"))
                    {
                        HttpResponse css = new HttpResponse();
                        css.additionalHeaders = new Dictionary<string, string>();
                        css.content = f.getLines();
                        css.contentType = "text/css";
                        f.Close();
                        return css;

                    }

                    else if (path.Contains(".js"))
                    {
                        HttpResponse css = new HttpResponse();
                        css.additionalHeaders = new Dictionary<string, string>();
                        css.content = f.getLines();
                        css.contentType = "application/javascript";
                        f.Close();
                        return css;
                    }
                    f.Close();

                }

                requestedPage = new Error404();
                return requestedPage.getPage(queryset, headers);
            }
        }
    }

    class Error404 : IPage
    {
        public string getName()
        {
            return "404";
        }

        public string getPath()
        {
            return "";
        }

        public HttpResponse getPage(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            HttpResponse resp = new HttpResponse();
            resp.additionalHeaders = new Dictionary<string, string>();
            resp.content = "404 not found!";
            resp.contentType = getContentType();

            return resp;
        }

        public string getContentType()
        {
            return "text/html";
        }

        public bool isVisible()
        {
            return false;
        }
    }

    class Homepage : HTMLPage
    {
        public override string getName()
        {
            return "Home";
        }

        public override string getPath()
        {
            return "/";
        }

        public override string getContent(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            StringBuilder S = new StringBuilder();
            S.Append(loadHeader());
            IFile p = new IFile("webfront\\main.html");
            S.Append(p.getLines());
            p.Close();
            S.Append(loadFooter());

            return S.ToString();
        }
    }

    class ServersJSON : IPage
    {
        public string getName()
        {
            return "Servers";
        }

        public string getPath()
        {
            return "/_servers";
        }

        public HttpResponse getPage(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            var info = new List<ServerInfo>();

            foreach (Server S in Manager.GetInstance().Servers)
            {
                ServerInfo eachServer = new ServerInfo();
                eachServer.serverName = S.getName();
                eachServer.serverPort = S.getPort();
                eachServer.maxPlayers = S.MaxClients;
                eachServer.mapName = S.CurrentMap.Alias;
                eachServer.gameType = Utilities.gametypeLocalized(S.getGametype());
                eachServer.currentPlayers = S.getPlayers().Count;
                eachServer.chatHistory = S.chatHistory;
                eachServer.players = new List<PlayerInfo>();
                foreach (Player P in S.getPlayers())
                {
                    PlayerInfo pInfo = new PlayerInfo();
                    pInfo.playerID = P.DatabaseID;
                    pInfo.playerName = P.Name;
                    pInfo.playerLevel = P.Level.ToString();
                    eachServer.players.Add(pInfo);
                }

                info.Add(eachServer);
            }


            HttpResponse resp = new HttpResponse();
            resp.contentType = getContentType();
            resp.content = Newtonsoft.Json.JsonConvert.SerializeObject(info);
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

    class Info : IPage
    {
        public string getName()
        {
            return "Info";
        }

        public string getPath()
        {
            return "/_info";
        }

        public HttpResponse getPage(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            ApplicationInfo info = new ApplicationInfo();
            info.name = "IW4MAdmin";
            info.version = Program.Version;

            HttpResponse resp = new HttpResponse();
            resp.contentType = getContentType();
            resp.content = Newtonsoft.Json.JsonConvert.SerializeObject(info);
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


    class ConsoleJSON : IPage
    {
        public string getName()
        {
            return "_Console";
        }

        public string getPath()
        {
            return "/_console";
        }

        public HttpResponse getPage(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            CommandInfo cmd = new CommandInfo();
            cmd.Result = new List<string>();

            if (querySet["command"] != null)
            {

                if (querySet["server"] != null)
                {
                    Server S = Manager.GetInstance().Servers.ToList().Find(x => (x.getPort().ToString() == querySet["server"]));

                    if (S != null)
                    {
                        Player admin = Manager.GetInstance().GetClientDatabase().GetPlayer(querySet["IP"]);

                        if (admin == null)
                            admin = new Player("RestUser", "-1", -1, (int)Player.Permission.User);

                        Event remoteEvent = new Event(Event.GType.Say, querySet["command"], admin, null, S);
                        remoteEvent.Remote = true;
                        admin.lastEvent = remoteEvent;

                        S.ExecuteEvent(remoteEvent);

                        while (S.commandResult.Count > 0)
                            cmd.Result.Add(S.commandResult.Dequeue());
                    }
                    else
                        cmd.Result.Add("Invalid server selected.");
                }
                else
                    cmd.Result.Add("Invalid server selected.");
            }

            else
            {
                cmd.Result.Add("No command entered.");
            }

            HttpResponse resp = new HttpResponse();
            resp.contentType = getContentType();
            resp.content = Newtonsoft.Json.JsonConvert.SerializeObject(cmd);
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


    class PenaltiesJSON : IPage
    {
        public string getName()
        {
            return "Penalties";
        }

        public string getPath()
        {
            return "/_penalties";
        }

        public HttpResponse getPage(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            int from = 0;
            if (querySet["from"] != null)
                from = Int32.Parse(querySet["from"]);
            List<Penalty> selectedPenalties;

            try
            {
                //selectedPenalties = Manager.GetInstance().Servers.First().Bans.OrderByDescending(x => x.When).ToList().GetRange(Convert.ToInt32(querySet["from"]), 15);
                selectedPenalties = ((Manager.GetInstance().GetClientPenalties()) as PenaltyList).AsChronoList(Convert.ToInt32(querySet["from"]), 15).OrderByDescending(b => b.When).ToList();
            }

            catch (Exception)
            {
                selectedPenalties = new List<Penalty>();
            }

            List<PenaltyInfo> info = new List<PenaltyInfo>();

            foreach (var p in selectedPenalties)
            {
                Player admin = Manager.GetInstance().GetClientDatabase().GetPlayer(p.PenaltyOriginID, 0);
                Player penalized = Manager.GetInstance().GetClientDatabase().GetPlayer(p.OffenderID, 0);
                if (admin == null && penalized == null)
                    continue;
                if (admin == null)
                    admin = new Player("Unknown", "-1", -1, (int)Player.Permission.Banned);
                PenaltyInfo pInfo = new PenaltyInfo();
                pInfo.adminName = admin.Name;
                pInfo.adminLevel = admin.Level.ToString();
                pInfo.penaltyReason = p.Reason;
                pInfo.penaltyTime = SharedLibrary.Utilities.timePassed(p.When);
                pInfo.penaltyType = p.BType.ToString();
                pInfo.playerName = penalized.Name;
                pInfo.playerID = penalized.DatabaseID;
                if (admin.NetworkID == penalized.NetworkID)
                {
                    pInfo.adminName = "IW4MAdmin";
                    pInfo.adminLevel = Player.Permission.Console.ToString();
                }
                info.Add(pInfo);
            }

            HttpResponse resp = new HttpResponse();
            resp.contentType = getContentType();
            resp.content = Newtonsoft.Json.JsonConvert.SerializeObject(info);
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

    class Penalties : HTMLPage
    {
        public override string getName()
        {
            return "Penalties";
        }

        public override string getPath()
        {
            return "/penalties";
        }

        public override string getContent(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            StringBuilder S = new StringBuilder();
            S.Append(loadHeader());

            IFile penalities = new IFile("webfront\\penalties.html");
            S.Append(penalities.getLines());
            penalities.Close();

            S.Append(loadFooter());

            return S.ToString();
        }
    }

    class WebConsole : HTMLPage
    {
        public override string getName()
        {
            return "Console";
        }

        public override string getPath()
        {
            return "/console";
        }

        public override string getContent(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            StringBuilder S = new StringBuilder();
            S.Append(loadHeader());

            IFile console = new IFile("webfront\\console.html");
            S.Append(console.getLines());
            console.Close();

            S.Append(loadFooter());

            return S.ToString();
        }
    }

    class Players : HTMLPage
    {
        public override string getName()
        {
            return "Players";
        }

        public override string getPath()
        {
            return "/players";
        }

        public override string getContent(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            StringBuilder S = new StringBuilder();
            S.Append(loadHeader());

            IFile penalities = new IFile("webfront\\players.html");
            S.Append(penalities.getLines());
            penalities.Close();

            S.Append(loadFooter());

            return S.ToString();
        }
    }

    class PubbansJSON : IPage
    {
        public string getName()
        {
            return "Public Ban List";
        }

        public string getPath()
        {
            return "/pubbans";
        }

        public HttpResponse getPage(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            HttpResponse resp = new HttpResponse();
            resp.contentType = getContentType();
            resp.content = Newtonsoft.Json.JsonConvert.SerializeObject(((Manager.GetInstance().GetClientPenalties()) as PenaltyList).AsChronoList(Convert.ToInt32(querySet["from"]), 15), Newtonsoft.Json.Formatting.Indented, new Newtonsoft.Json.JsonConverter[] { new Newtonsoft.Json.Converters.StringEnumConverter() });
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

    class Pages : IPage
    {
        public string getName()
        {
            return "Pages";
        }

        public string getPath()
        {
            return "/pages";
        }

        public HttpResponse getPage(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            List<PageInfo> pages = new List<PageInfo>();

            foreach (var p in SharedLibrary.WebService.pageList.Where(x => x.isVisible()))
            {
                if (p == this)
                    continue;

                PageInfo pi = new PageInfo();
                pi.pagePath = p.getPath();
                // pi.pageType = p.getPage(querySet, headers).contentType;
                pi.pageName = p.getName();
                pi.visible = p.isVisible();
                pages.Add(pi);
            }

            HttpResponse resp = new HttpResponse();
            resp.contentType = getContentType();
            resp.content = Newtonsoft.Json.JsonConvert.SerializeObject(pages);
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

    class GetPlayer : IPage
    {
        public string getContentType()
        {
            return "application/json";
        }

        public string getPath()
        {
            return "/getplayer";
        }

        public string getName()
        {
            return "GetPlayer";
        }

        public HttpResponse getPage(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            List<PlayerInfo> pInfo = new List<PlayerInfo>();
            List<Player> matchedPlayers = new List<Player>();
            HttpResponse resp = new HttpResponse();
            resp.contentType = getContentType();
            resp.additionalHeaders = new Dictionary<string, string>();

            bool authed = Manager.GetInstance().GetClientDatabase().GetAdmins().FindAll(x => x.IP == querySet["IP"]).Count > 0;
            bool recent = false;

            if (querySet["id"] != null)
            {
                matchedPlayers.Add(Manager.GetInstance().GetClientDatabase().GetPlayer(Convert.ToInt32(querySet["id"])));
            }

            else if (querySet["npID"] != null)
            {
                matchedPlayers.Add(Manager.GetInstance().GetClientDatabase().GetPlayers(new List<string> { querySet["npID"] }).First());
            }

            else if (querySet["name"] != null)
            {
                matchedPlayers = Manager.GetInstance().GetClientDatabase().FindPlayers(querySet["name"]);
            }

            else if (querySet["recent"] != null)
            {
                 matchedPlayers = Manager.GetInstance().GetClientDatabase().GetRecentPlayers();
                recent = true;
            }

            if (matchedPlayers != null && matchedPlayers.Count > 0)
            {
                foreach (var pp in matchedPlayers)
                {
                    if (pp == null) continue;
                    PlayerInfo eachPlayer = new PlayerInfo();
                    eachPlayer.playerID = pp.DatabaseID;
                    eachPlayer.playerIP = pp.IP;
                    eachPlayer.playerLevel = pp.Level.ToString();
                    eachPlayer.playerName = pp.Name;
                    eachPlayer.playernpID = pp.NetworkID;
                    eachPlayer.forumID = -1;
                    eachPlayer.authed = authed;
                    eachPlayer.showV2Features = false;

                    if (!recent)
                    {
                        foreach (var a in Manager.GetInstance().Servers.First().GetAliases(pp))
                        {
                            eachPlayer.playerAliases = a.Names;
                            eachPlayer.playerIPs = a.IPS;
                        }
                    }

                    eachPlayer.playerConnections = pp.Connections;
                    eachPlayer.lastSeen = Utilities.timePassed(pp.LastConnection);
                    pInfo.Add(eachPlayer);

                }

                resp.content = Newtonsoft.Json.JsonConvert.SerializeObject(pInfo);
                return resp;
            }

            resp.content = Newtonsoft.Json.JsonConvert.SerializeObject(null);
            return resp;
        }

        public bool isVisible()
        {
            return false;
        }
    }

    [Serializable]
    struct ServerInfo
    {
        public string serverName;
        public int serverPort;
        public string mapName;
        public string gameType;
        public int currentPlayers;
        public int maxPlayers;
        public List<Chat> chatHistory;
        public List<PlayerInfo> players;
    }

    [Serializable]
    struct ApplicationInfo
    {
        public double version;
        public string name;
    }

    [Serializable]
    struct PageInfo
    {
        public string pageName;
        public string pagePath;
        public string pageType;
        public bool visible;
    }

    [Serializable]
    struct PlayerInfo
    {
        public string playerName;
        public int playerID;
        public string playerLevel;
        public string playerIP;
        public string playernpID;
        public Int64 forumID;
        public List<string> playerAliases;
        public List<string> playerIPs;
        public int playerConnections;
        public string lastSeen;
        public bool showV2Features;
        public bool authed;
    }

    [Serializable]
    struct PenaltyInfo
    {
        public string playerName;
        public int playerID;
        public string adminName;
        public string adminLevel;
        public string penaltyType;
        public string penaltyReason;
        public string penaltyTime;
    }

    [Serializable]
    struct CommandInfo
    {
        public List<string> Result;
    }
}