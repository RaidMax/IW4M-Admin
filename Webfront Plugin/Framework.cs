using System;
using System.Collections.Generic;
using SharedLibrary;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Specialized;

namespace Webfront_Plugin
{
    class Framework
    {
        private List<Server> activeServers;

        public Framework()
        {
            activeServers = new List<Server>();
        }

        public void addServer(Server S)
        {
            activeServers.Add(S);
        }

        public void removeServer(Server S)
        {
            if (activeServers.Contains(S))
                activeServers.Remove(S);
        }

        private String processTemplate(String Input, String Param)
        {
            try
            {
                Server requestedServer = null;
                int requestPageNum = 0;
                int ID = 0;
                String Query = "";

                if (Param != null)
                {
                    NameValueCollection querySet = System.Web.HttpUtility.ParseQueryString(Param);

                    if (querySet["server"] != null)
                        requestedServer = activeServers.Find(x => x.pID() == Int32.Parse(querySet["server"]));
                    else
                        requestedServer = activeServers.First();
                    
                    if (querySet["page"] != null)
                        requestPageNum = Int32.Parse(querySet["page"]);

                    if (querySet["id"] != null)
                        ID = Int32.Parse(querySet["id"]);

                    if (querySet["query"] != null)
                        Query = querySet["query"];
                }

                String Pattern = @"\{\{.+\}\}";
                Regex Search = new Regex(Pattern, RegexOptions.IgnoreCase);

                MatchCollection Matches = Search.Matches(Input);

                foreach (Match match in Matches)
                {
                    Input = processReplacements(Input, match.Value, requestPageNum, ID, Query, requestedServer);
                }

                return Input;
            }

            catch (Exception E)
            {
                Page Error = new error();
                return Error.Load().Replace("{{ERROR}}", E.Message);
            }
        }

        private String parsePagination(int totalItems, int itemsPerPage, int currentPage, String Page)
        {
            StringBuilder output = new StringBuilder();

            output.Append("<div id=pages>");

            if (currentPage > 0)
                output.AppendFormat("<a href=/{0}?page={1}>PREV</a>", Page, currentPage - 1);

            double totalPages = Math.Ceiling(((float)totalItems / itemsPerPage));

            output.Append("<span id=pagination>" + (currentPage + 1) + "/" + totalPages + "</span>");

            if ((currentPage + 1) < totalPages)
                output.AppendFormat("<a href=/{0}?page={1}>NEXT</a>", Page, currentPage + 1);

            output.Append("</div>");

            return output.ToString();
        }

        private String processReplacements(String Input, String Macro, int curPage, int ID, String Query, params Server[] Servers)
        {
            bool Authenticated = false;
            bool UserPrivelege = false;

            if (Servers[0] != null && Manager.lastIP != null)
            {
                Player User = Servers[0].clientDB.getPlayer(Manager.lastIP.ToString());
                if (User != null && User.Level > Player.Permission.Flagged)
                    Authenticated = true;
                if (User != null && User.Level == Player.Permission.User)
                    UserPrivelege = true;
            }

            if (Macro.Length < 5)
                return "";

            String Looking = Macro.Substring(2, Macro.Length - 4);

            if (Looking == "SERVERS")
            {
                int cycleFix = 0;
                StringBuilder buffer = new StringBuilder();
              
                foreach (Server S in activeServers)
                {
                    StringBuilder players = new StringBuilder();
                    if (S.getClientNum() > 0)
                    {
                        int count = 0;
                        double currentPlayers = S.statusPlayers.Count;

                        players.Append("<table cellpadding='0' cellspacing='0' class='players'>");

                        foreach (Player P in S.getPlayers())
                        {
                            if (P == null)
                                continue;

                            if (count % 2 == 0)
                            {
                                switch (cycleFix)
                                {
                                    case 0:
                                        players.Append("<tr class='row-grey'>");
                                        cycleFix = 1;
                                        break;
                                    case 1:
                                        players.Append("<tr class='row-white'>");
                                        cycleFix = 0;
                                        break;
                                }
                            }

                            players.AppendFormat("<td><a href='/player?id={0}'>{1}</a></td>", P.databaseID, SharedLibrary.Utilities.nameHTMLFormatted(P));

                            if (count % 2 != 0)
                            {
                                players.Append("</tr>");
                            }

                            count++;

                        }
                        players.Append("</table>");
                    }
                    buffer.AppendFormat(@"<table cellpadding=0 cellspacing=0 class=server>
                                                <tr>
                                                    <th class=server_title><span>{0}</span></th>
                                                    <th class=server_map><span>{1}</span></th>
                                                    <th class=server_players><span>{2}</span></th>
                                                    <th class=server_gametype><span>{3}</span></th>
                                                    <th><a href=/bans>Bans</a></th>
                                                    <th><a class='history' href='/graph?server={4}'>History</a></th>
                                                 </tr>
                                             </table>
                                                    {5}",
                                          
                                         S.getName(), S.getMap(), S.getClientNum() + "/" + S.getMaxClients(), SharedLibrary.Utilities.gametypeLocalized(S.getGametype()), S.pID(), players.ToString());

                    if (S.getClientNum() > 0)
                    {
                        buffer.AppendFormat("<div class='chatHistory' id='chatHistory_{0}'></div><script type='text/javascript'>$( document ).ready(function() {{ setInterval({1}loadChatMessages({0}, '#chatHistory_{0}'){1}, 2500); }});</script><div class='null' style='clear:both;'></div>", S.pID(), '\"');
                        if (UserPrivelege || Authenticated)
                            buffer.AppendFormat("<form class='chatOutFormat' action={1}javascript:chatRequest({0}, 'chatEntry_{0}'){1}><input class='chatFormat_text' type='text' placeholder='Enter a message...' id='chatEntry_{0}'/><input class='chatFormat_submit' type='submit'/></form>", S.pID(), '\"');
                    }
                    buffer.Append("<hr/>");
                }
                return Input.Replace(Macro, buffer.ToString());
            }

            if(Looking == "CHAT")
            {
                StringBuilder chatMessages = new StringBuilder();
                chatMessages.Append("<table id='table_chatHistory'>");
                if (Servers.Length > 0 && Servers[0] != null)
                {
                    foreach (Chat Message in Servers[0].chatHistory)
                        chatMessages.AppendFormat("<tr><td class='chat_name' style='text-align: left;'>{0}</td><td class='chat_message'>{1}</td><td class='chat_time' style='text-align: right;'>{2}</td></tr>", SharedLibrary.Utilities.nameHTMLFormatted(Message.Origin), Message.Message, Message.timeString());
                }
                
                chatMessages.Append("</table>");
                return chatMessages.ToString();
            }

            if (Looking == "PLAYER")
            {
                StringBuilder buffer = new StringBuilder();
                Server S = activeServers[0];

                buffer.Append("<table class='player_info'><tr><th>Name</th><th>Aliases</th><th>IP</th><th>Rating</th><th>Level</th><th>Connections</th><th>Last Seen</th><th>Profile</th>");
                List<Player> matchingPlayers = new List<Player>();

                if (ID > 0)
                    matchingPlayers.Add(S.clientDB.getPlayer(ID));

                else if (Query.Length > 2)
                {
                    matchingPlayers = S.clientDB.findPlayers(Query);

                    if (matchingPlayers == null)
                        matchingPlayers = new List<Player>();
                   
                    List<int> matchedDatabaseIDs = new List<int>();

                    foreach (Aliases matchingAlias in S.aliasDB.findPlayers(Query))
                        matchedDatabaseIDs.Add(matchingAlias.Number);

                    foreach (Player matchingP in S.clientDB.getPlayers(matchedDatabaseIDs))
                    {
                        if (matchingPlayers.Find(x => x.databaseID == matchingP.databaseID) == null)
                            matchingPlayers.Add(matchingP);
                    }
                }

                if (matchingPlayers == null)
                    buffer.Append("</table>");

                else
                {
                    foreach (Player Player in matchingPlayers)
                    {
                        if (Player == null)
                            continue;

                        buffer.Append("<tr>");
                        StringBuilder Names = new StringBuilder();

                        
                        List<String> nameAlias = new List<String>();
                        List<String> IPAlias = new List<String>();
                        StringBuilder IPs = new StringBuilder();

                        if (Authenticated)
                        {
                            List<Aliases> allAlliases = S.getAliases(Player);

                            foreach (Aliases A in allAlliases)
                            {

                                foreach (String Name in A.Names.Distinct())
                                    nameAlias.Add(Name);

                                foreach (String IP in A.IPS.Distinct())
                                    IPAlias.Add(IP);

                            }

                            Names.Append("<a href='#' class='pseudoLinkAlias'>Show Aliases</a>");
                            Names.Append("<div class='playerAlias'>");
                            foreach (String Name in nameAlias.Distinct())
                                Names.AppendFormat("<span>{0}</span><br/>", Utilities.stripColors(Name));
                            Names.Append("</div>");

                            IPs.Append("<a href='#' class='pseudoLinkIP'>Show IPs</a>");
                            IPs.Append("<div class='playerIPs'>");
                            foreach (String IP in IPAlias)
                                IPs.AppendFormat("<span>{0}</span><br/>", IP);
                            IPs.Append("</div>");
                        }
   
                        if (!Authenticated)
                        {
                            Names.Append("Hidden");
                            IPs.Append("Hidden");
                        }
                            
                        Int64 forumID = 0;
                        if (Player.npID.Length == 16)
                        {
                            forumID = Int64.Parse(Player.npID.Substring(0, 16), System.Globalization.NumberStyles.AllowHexSpecifier);
                            forumID = forumID - 76561197960265728;
                        }

                        String Screenshot = String.Empty;

                        //if (logged)
                        Screenshot = String.Format("<a href='http://server.nbsclan.org/screen.php?id={0}&name={1}' target='_blank'><div style='background-image:url(http://server.nbsclan.org/shutter.png); width: 20px; height: 20px;float: right; position:relative; right: 21%; background-size: contain;'></div></a>", forumID, Player.Name);

                        buffer.AppendFormat("<td><a style='float: left;' href='{9}'>{0}</a>{10}</td><td>{1}</td><td>{2}</td><td>{3}</td><td>{4}</td><td>{5}</td><td>{6} ago</td><td><a href='https://repziw4.de/memberlist.php?mode=viewprofile&u={7}'>{8}</a></td>", Player.Name, Names, IPs, 0, SharedLibrary.Utilities.levelHTMLFormatted(Player.Level), Player.Connections, Player.getLastConnection(), forumID, Player.Name, "/player?id=" + Player.databaseID, Screenshot);
                        buffer.Append("</tr>");
                    }

                    buffer.Append("</table>");
                    return Input.Replace(Macro, buffer.ToString());
                }
            }

            if (Looking == "BANS")
            {
                StringBuilder buffer = new StringBuilder();
                Server S = activeServers[0];

                buffer.Append("<table cellspacing=0 class=bans>");
                int limitPerPage = 30;
                int Pagination =  curPage;
                int totalBans = S.Bans.Count;
                int range;
                int start = Pagination * limitPerPage;
                int cycleFix = 0;

                if (totalBans <= limitPerPage)
                    range = totalBans - 1;
                else if ((totalBans - start) < limitPerPage)
                    range = (totalBans - start);
                else
                    range = limitPerPage;

                List<Ban> Bans = new List<Ban>();

                if (totalBans > 0)
                    Bans = S.Bans.GetRange(start, range).OrderByDescending(x => x.When).ToList();

                if (Bans.Count == 0)
                    buffer.Append("<span style='font-size: 16pt;'>No bans yet.</span>");

                else
                {
                    buffer.Append("<h1 style=margin-top: 0;>{{TIME}}</h1><hr /><tr><th>Name</th><th style=text-align:left;>Offense</th><th style=text-align:left;>Banned By</th><th style='width: 175px; text-align:right;padding-right: 80px;'>Time</th></tr>");

                    if (Bans[0] != null)
                        buffer = buffer.Replace("{{TIME}}", "From " + SharedLibrary.Utilities.timePassed(Bans[0].When) + " ago" + " &mdash; " + totalBans + " total");

                    List<String> npIDs = new List<string>();

                    foreach (Ban B in Bans)
                        npIDs.Add(B.npID);
          

                    List<Player> bannedPlayers = S.clientDB.getPlayers(npIDs);

                    for (int i = 0; i < Bans.Count-1; i++)
                    {
                        if (Bans[i] == null)
                            continue;

                        Player P = bannedPlayers.Where(x => x.npID == Bans[i].npID).First();
                        Player B;

                        if (P.npID == Bans[i].bannedByID || Bans[i].bannedByID == "")
                            B = new Player("IW4MAdmin", "", 0, SharedLibrary.Player.Permission.Banned, 0, "", 0, "");

                        else
                            B = S.clientDB.getPlayer(Bans[i].bannedByID, -1);

                        if (P == null)
                            P = new Player("Unknown", "n/a", 0, 0, 0, "Unknown", 0, "");
                        if (B == null)
                            B = new Player("Unknown", "n/a", 0, 0, 0, "Unknown", 0, "");

                        if (P.lastOffense == String.Empty)
                            P.lastOffense = "Evade";

                        if (P != null && B != null)
                        {

                            String Prefix;
                            if (cycleFix % 2 == 0)
                                Prefix = "class=row-grey";
                            else
                                Prefix = "class=row-white";

                            String Link = "/player?id=" + P.databaseID;
                            buffer.AppendFormat("<tr {4}><td><a href='{5}'>{0}</a></th><td style='border-left: 3px solid #bbb; text-align:left;'>{1}</td><td style='border-left: 3px solid #bbb;text-align:left;'>{2}</td><td style='width: 175px; text-align:right;'>{3}</td></tr></div>", P.Name, P.lastOffense, SharedLibrary.Utilities.nameHTMLFormatted(B), Bans[i].getWhen(), Prefix, Link);
                            cycleFix++;
                        }
                    }
                }
                buffer.Append("</table><hr/>");

                if (totalBans > limitPerPage)
                    buffer.Append(parsePagination(totalBans, limitPerPage, Pagination, "bans"));

                return Input.Replace(Macro, buffer.ToString());
            }

            if (Looking == "GRAPH")
            {
                StringBuilder buffer = new StringBuilder();
                buffer.Append("<script type='text/javascript' src='//www.google.com/jsapi'></script><div id='chart_div'></div>");
                buffer.Append("<script> var players = [");
                int count = 1;
                List<PlayerHistory> run = Servers[0].playerHistory.ToList();
                foreach (PlayerHistory i in run) //need to reverse for proper timeline
                {
                    buffer.AppendFormat("[new Date({0}, {1}, {2}, {3}, {4}), {5}]", i.When.Year, i.When.Month - 1, i.When.Day, i.When.Hour, i.When.Minute, i.Players);
                    if (count < run.Count)
                        buffer.Append(",\n");
                    count++;
                }
                buffer.Append("];\n");
                buffer.Append("</script>");
                return Input.Replace(Macro, buffer.ToString());
            }

            if (Looking == "TITLE")
                return Input.Replace(Macro, "IW4MAdmin by RaidMax");

            if (Looking == "VERSION")
                return Input.Replace(Macro, "0.9.5");

            return "PLACEHOLDER";
        
        }

        public String processRequest(Kayak.Http.HttpRequestHead request)
        {
            Page requestedPage = new notfound();
            Page Header = new header();
            Page Footer = new footer();

            if (request.Path == "/")
                requestedPage = new main();
           
            else
            {
                string p = request.Path.ToLower().Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries)[0];
                switch (p)
                {
                    case "bans":
                        requestedPage = new bans();
                        break;
                    case "player":
                        requestedPage = new player();
                        break;
                    case "graph":
                        requestedPage = new graph();
                        return processTemplate(requestedPage.Load(), request.QueryString);
                    case "chat":
                        requestedPage = new chat();
                        return processTemplate(requestedPage.Load(), request.QueryString);
                    case "error":
                        requestedPage = new error();
                        break;
                    default:
                        requestedPage = new notfound();
                        break;
                }
            }

            return processTemplate(Header.Load(), null) + processTemplate(requestedPage.Load(), request.QueryString) + processTemplate(Footer.Load(), null);
        }
    }

    abstract class Page
    {
        public abstract String Load();
        public abstract String Name { get; }

        protected String loadHTML()
        {
            IFile HTML = new IFile("webfront\\" + this.Name + ".html");
            String Contents = HTML.getLines();
            HTML.Close();
            return Contents;
        }
    }

    class notfound : Page
    {
        public override String Name
        {
            get { return "notfound"; }
        }

        public override String Load()
        {
            return loadHTML();
        }
    }

    class main : Page
    {
        public override String Name
        {
            get { return "main"; }
        }

        public override String Load()
        {
            return loadHTML();
        }
    }

    class bans : Page
    {
        public override String Name
        {
            get { return "bans"; }
        }

        public override String Load()
        {
            return loadHTML();
        }
    }

    class header : Page
    {
        public override String Name
        {
            get { return "header"; }
        }

        public override String Load()
        {
            return loadHTML();
        }
    }

    class footer : Page
    {
        public override String Name
        {
            get { return "footer"; }
        }

        public override String Load()
        {
            return loadHTML();
        }
    }

    class player : Page
    {
        public override String Name
        {
            get { return "player"; }
        }

        public override String Load()
        {
            return loadHTML();
        }
    }

    class graph : Page
    {
        public override String Name
        {
            get { return "graph"; }
        }

        public override String Load()
        {
            return loadHTML();
        }
    }

    class chat : Page
    {
        public override String Name
        {
            get { return "chat"; }
        }

        public override String Load()
        {
            return "{{CHAT}}";
        }
    }

    class error : Page
    {
        public override String Name
        {
            get { return "error"; }
        }

        public override String Load()
        {
            return loadHTML();
        }
    }
}
