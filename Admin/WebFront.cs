using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak;
using Kayak.Http;
using System.Net;


namespace IW4MAdmin_Web
{
    class WebFront
    {
        public enum Page
        {
            main,
            stats,
            bans
        }

        public WebFront()
        {

        }

        public void Init()
        {
            webSchedule = KayakScheduler.Factory.Create(new SchedulerDelegate());
            webServer = KayakServer.Factory.CreateHttp(new RequestDelegate(), webSchedule);
           
            using (webServer.Listen(new IPEndPoint(IPAddress.Any, 1624)))
            {
                // runs scheduler on calling thread. this method will block until
                // someone calls Stop() on the scheduler.
                webSchedule.Start();
            }
        }

        private IScheduler webSchedule;
        private IServer webServer;
    }

    static class Macro
    {
        static public String parsePagination(int server, int totalItems, int itemsPerPage, int currentPage, String Page)
        {
            StringBuilder output = new StringBuilder();

            output.Append("<div id=pages>");

            if ( currentPage > 0)
                output.AppendFormat("<a href=/{0}/{1}/?{2}>PREV</a>", server, currentPage - 1, Page);
            double totalPages = Math.Ceiling(((float)totalItems / itemsPerPage));
            output.Append("<span id=pagination>" + (currentPage + 1) + "/" + totalPages + "</span>");
            if ((currentPage + 1) < totalPages)
                output.AppendFormat("<a href=/{0}/{1}/?{2}>NEXT</a>", server, currentPage + 1, Page);
            output.Append("</div>");

            return output.ToString();
        }

        static public String parseMacros(String input, WebFront.Page Page, int Pagination, int server)
        {
            StringBuilder buffer = new StringBuilder();
            switch (input)
            {
                case "SERVERS":
                    var Servers = IW4MAdmin.Program.Servers;
                    for (int i = 0; i < Servers.Count; i++)
                    {
                        StringBuilder players = new StringBuilder();
                        if (Servers[i].getClientNum() < 1)
                            players.Append("<th>No Players</th>");
                        else
                        {
                            int count = 0;
                            foreach (IW4MAdmin.Player P in Servers[i].statusPlayers.Values)
                            {
                                if (count > 0 && count % 6 == 0)
                                    players.Append("</tr><tr>");
                                players.AppendFormat("<td>{0}</td>", P.getName());
                                count++;
                            }
                        }
                        buffer.AppendFormat(@"<table cellpadding=0 cellspacing=0 class=server>
                                                <tr>
                                                    <th class=server_title><span>{0}</span></th>
                                                    <th class=server_map><span>{1}</span></th>
                                                    <th class=server_players><span>{2}</span></th>
                                                    <th  class=server_gametype><span>{3}</span></th>
                                                    <th><a href=/{4}/0/?stats>Stats</a></th>
                                                    <th><a href=/{4}/0/?bans>Bans</a></th>
                                                 </tr>
                                                </table>
                                              <table class=players>
                                                 <tr>
                                                    {5}
                                                 </tr>
                                             </table>
                                            <hr/>", 
                                             Servers[i].getName(), Servers[i].getMap(), Servers[i].getClientNum() + "/" + Servers[i].getMaxClients(), IW4MAdmin.Utilities.gametypeLocalized(Servers[i].getGametype()), i, players.ToString());
                    }
                    return buffer.ToString();
                case "TITLE":
                    return "IW4M Administration";
                case "BANS":
                    buffer.Append("<table cellspacing=0 class=bans>");
                    int totalBans = IW4MAdmin.Program.Servers[0].Bans.Count;
                    int range;
                    int start = Pagination*30 + 1;
                    int cycleFix = 0;

                    if (totalBans <= 30)
                        range = totalBans - 1;
                    else if ((totalBans - start) < 30)
                        range = (totalBans - start);
                    else
                        range = 30;

                    List<IW4MAdmin.Ban> Bans = new List<IW4MAdmin.Ban>();

                    if (totalBans > 0)
                        Bans = IW4MAdmin.Program.Servers[0].Bans.GetRange(start, range).OrderByDescending(x => x.getTime()).ToList();
                    else
                        Bans.Add(new IW4MAdmin.Ban("No Bans", "0", "0", DateTime.Now, ""));


                    buffer.Append("<h1 style=margin-top: 0;>{{TIME}}</h1><hr /><tr><th>Name</th><th style=text-align:left;>Offense</th><th style=text-align:left;>Banned By</th><th style='width: 175px; text-align:right;padding-right: 80px;'>Time</th></tr>");

                    if (Bans[0] != null)
                        buffer = buffer.Replace("{{TIME}}", "From " + IW4MAdmin.Utilities.timePassed(Bans[0].getTime()) + " ago" + " &mdash; " + totalBans + " total");
             
                    for (int i = 0; i < Bans.Count; i++)
                    {
                        if (Bans[i] == null)
                            continue;

                        IW4MAdmin.Player P = IW4MAdmin.Program.Servers[0].clientDB.getPlayer(Bans[i].getID(), -1);
                        IW4MAdmin.Player B = IW4MAdmin.Program.Servers[0].clientDB.getPlayer(Bans[i].getBanner(), -1);

                        if (P == null)
                            P = new IW4MAdmin.Player("Unknown", "n/a", 0, 0, 0, "Unknown", 0, "");
                        if (B == null)
                            B = new IW4MAdmin.Player("Unknown", "n/a", 0, 0, 0, "Unknown", 0, "");

                        if (P.getLastO() == String.Empty)
                            P.LastOffense = "Evade";

                        if (P != null && B != null)
                        {
                            if (B.getID() == P.getID())
                                B.updateName("IW4MAdmin"); // shh it will all be over soon

                            String Prefix;
                            if (cycleFix % 2 == 0)
                                Prefix = "class=row-grey";
                            else
                                Prefix = "class=row-white";
                            buffer.AppendFormat("<tr {4}><td>{0}</th><td style='border-left: 3px solid #bbb; text-align:left;'>{1}</td><td style='border-left: 3px solid #bbb;text-align:left;'>{2}</td><td style='width: 175px; text-align:right;'>{3}</td></tr></div>", P.getName(), P.getLastO(), IW4MAdmin.Utilities.nameHTMLFormatted(B), Bans[i].getWhen(), Prefix);
                            cycleFix++;
                        }
                    }
                    buffer.Append("</table><hr/>");
 
                    buffer.Append(parsePagination(server, IW4MAdmin.Program.Servers[0].Bans.Count, 30, Pagination, "bans"));
                    return buffer.ToString();
                case "PAGE":
                    buffer.Append("<div id=pages>");
                    
                    return buffer.ToString();
                case "STATS":
                    int totalStats = IW4MAdmin.Program.Servers[server].statDB.totalStats();
                    buffer.Append("<h1 style='margin-top: 0;'>Starting at #{{TOP}}</h1><hr />");
                    buffer.Append("<table style='width:100%' cellspacing=0 class=stats>");
 
                    start = Pagination*30;
                    if (totalStats <= 30)
                        range = totalStats - 1;
                    else if ((totalStats - start) < 30 )
                        range = (totalStats - start);
                    else
                        range = 30;
                    List<IW4MAdmin.Stats> Stats = IW4MAdmin.Program.Servers[server].statDB.getMultipleStats(start, range).OrderByDescending(x => x.Skill).ToList();
                    buffer.Append("<tr><th style=text-align:left;>Name</th><th style=text-align:left;>Kills</th><th style=text-align:left;>Deaths</th><th style=text-align:left;>KDR</th><th style='width: 175px; text-align:right;'>Rating</th></tr>");
                    cycleFix = 0;
                    for (int i = 0; i < totalStats; i++)
                    {
                        if (i >= Stats.Count -1 || Stats[i] == null )
                            continue;

                        IW4MAdmin.Player P = IW4MAdmin.Program.Servers[server].clientDB.getPlayer(Stats[i].statIndex);

                        if (P == null)
                            continue;

                        P.stats = Stats[i];


                        if (P.stats != null)
                        {
                            String Prefix;
                            if (cycleFix % 2 == 0)
                                Prefix = "class=row-grey";
                            else
                                Prefix = "class=row-white";
                            buffer.AppendFormat("<tr {5}><td>{0}</th><td style='border-left: 3px solid #bbb; text-align:left;'>{1}</td><td style='border-left: 3px solid #bbb;text-align:left;'>{2}</td><td style='border-left: 3px solid #bbb;text-align:left;'>{3}</td><td style='width: 175px; text-align:right;'>{4}</td></tr></div>", P.getName(), P.stats.Kills, P.stats.Deaths, P.stats.KDR, P.stats.Skill, Prefix);
                            cycleFix++;
                        }
                    }
                    buffer.Append("</table><hr/>");
                    buffer.Append(parsePagination(server, totalStats, 30, Pagination, "stats"));
                    return buffer.ToString().Replace("{{TOP}}", (start + 1).ToString());
                default:
                    return input;
            }
        }

        static public String findMacros(String input, int pageNumber, int server, WebFront.Page page)
        {
            String output = input;

            switch (page)
            {
                case WebFront.Page.main:
                    output = output.Replace("{{SERVERS}}", parseMacros("SERVERS", page, pageNumber, server));
                    break;
                case WebFront.Page.bans:
                    output = output.Replace("{{BANS}}", parseMacros("BANS", page, pageNumber, server));
                    break;
                case WebFront.Page.stats:
                    output = output.Replace("{{STATS}}", parseMacros("STATS", page, pageNumber, server));
                    break;
            }

            //output = output.Replace("{{PAGE}}", parseMacros("PAGE", page, pageNumber, server));
            
            //output = output.Replace("{{SERVERS}}", parseMacros("SERVERS", 0));
            //output = output.Replace("{{BANS}}", parseMacros("BANS", page));
            output = output.Replace("{{TITLE}}", "IW4M Administration");
            //output = output.Replace("{{PAGE}}", parseMacros("PAGE", page));
            //output = output.Replace("{{STATS}}", parseMacros("STATS", page));

            return output;
        }
    }

    class SchedulerDelegate : ISchedulerDelegate
    {
        public void OnException(IScheduler scheduler, Exception e)
        {
            Console.WriteLine(e.InnerException.Message);
            Console.Write(e.InnerException);
            e.DebugStackTrace();
        }

        public void OnStop(IScheduler scheduler)
        {

        }
    }

    class RequestDelegate : IHttpRequestDelegate
    {
        public void OnRequest(HttpRequestHead request, IDataProducer requestBody, IHttpResponseDelegate response)
        {
            if (request.Uri.StartsWith("/"))
            {
                Console.WriteLine("[WEBFRONT] Processing Request for " + request.Uri);             
                var body = String.Empty;

                if (request.Uri.StartsWith("/"))
                {
                    IW4MAdmin.file Header = new IW4MAdmin.file("webfront\\header.html");
                    var header = Header.getLines();
                    Header.Close();

                    String[] req = request.Path.Split(new char[] {'/'}, StringSplitOptions.RemoveEmptyEntries);


                    int server = 0;
                    int page = 0;

                    if (req.Length > 1)
                    {
                        Int32.TryParse(req[0], out server);
                        Int32.TryParse(req[1], out page);
                    }

                    if (request.QueryString == "bans")
                    {
                        IW4MAdmin.file Bans = new IW4MAdmin.file("webfront\\bans.html");
                        var bans = Bans.getLines();
                        Bans.Close();
                        body = Macro.findMacros((header + bans), page, server, WebFront.Page.bans);
                    }

                    else if (request.QueryString == "stats")
                    {
                        IW4MAdmin.file Stats = new IW4MAdmin.file("webfront\\stats.html");
                        var stats = Stats.getLines();
                        Stats.Close();
                        body = Macro.findMacros(header + stats, page, server, WebFront.Page.stats);
                    }

                    else
                    {
                        IW4MAdmin.file Main = new IW4MAdmin.file("webfront\\main.html");
                        var main = Main.getLines();
                        Main.Close();
                        body = Macro.findMacros(header + main, 0, server, WebFront.Page.main);
                    }               
                }

                /*var body = string.Format(
                    "Uri: {0}\r\nPath: {1}\r\nQuery:{2}\r\nFragment: {3}\r\n",
                    request.Uri,
                    request.Path,
                    request.QueryString,
                    request.Fragment);*/

                var headers = new HttpResponseHead()
                {
                    Status = "200 OK",
                    Headers = new Dictionary<string, string>() 
                    {
                        { "Content-Type", "text/html" },
                        { "Content-Length", body.Length.ToString() },
                    }
                };

                response.OnResponse(headers, new BufferedProducer(body));
            }

            else
            {
                var responseBody = "The resource you requested ('" + request.Uri + "') could not be found.";
                var headers = new HttpResponseHead()
                {
                    Status = "404 Not Found",
                    Headers = new Dictionary<string, string>()
                    {
                        { "Content-Type", "text/text" },
                        { "Content-Length", responseBody.Length.ToString() }
                    }
                };
                var body = new BufferedProducer(responseBody);

                response.OnResponse(headers, body);
            }
        }

        class BufferedProducer : IDataProducer
        {
            ArraySegment<byte> data;

            public BufferedProducer(string data) : this(data, Encoding.UTF8) { }
            public BufferedProducer(string data, Encoding encoding) : this(encoding.GetBytes(data)) { }
            public BufferedProducer(byte[] data) : this(new ArraySegment<byte>(data)) { }
            public BufferedProducer(ArraySegment<byte> data)
            {
                this.data = data;
            }

            public IDisposable Connect(IDataConsumer channel)
            {
                // null continuation, consumer must swallow the data immediately.
                channel.OnData(data, null);
                channel.OnEnd();
                return null;
            }
        }

        class BufferedConsumer : IDataConsumer
        {
            List<ArraySegment<byte>> buffer = new List<ArraySegment<byte>>();
            Action<string> resultCallback;
            Action<Exception> errorCallback;

            public BufferedConsumer(Action<string> resultCallback, Action<Exception> errorCallback)
            {
                this.resultCallback = resultCallback;
                this.errorCallback = errorCallback;
            }
            public bool OnData(ArraySegment<byte> data, Action continuation)
            {
                // since we're just buffering, ignore the continuation. 
                buffer.Add(data);
                return false;
            }
            public void OnError(Exception error)
            {
                errorCallback(error);
            }

            public void OnEnd()
            {
                // turn the buffer into a string. 
                var str = buffer
                    .Select(b => Encoding.UTF8.GetString(b.Array, b.Offset, b.Count))
                    .Aggregate((result, next) => result + next);

                resultCallback(str);
            }
        } 
    }
 }
