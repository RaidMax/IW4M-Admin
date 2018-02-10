using SharedLibrary;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatsPlugin.Pages
{
    class LiveStatsJson : IPage
    {
        public string GetName() => "Kill Stats JSON";
        public string GetPath() => "/_killstats";
        public string GetContentType() => "application/json";
        public bool Visible() => false;

        public async Task<HttpResponse> GetPage(NameValueCollection querySet, IDictionary<string, string> headers)
        {
            // todo: redo this
            return await Task.FromResult(new HttpResponse());
            /*int selectCount = Stats.MAX_KILLEVENTS;

            if (querySet.Get("count") != null)
                selectCount = Int32.Parse(querySet.Get("count"));

            HttpResponse resp = new HttpResponse()
            {
                contentType = GetContentType(),
                content = new
                {
                    Servers = Stats.ManagerInstance.GetServers().Select(s => new
                    {
                        ServerName = s.Hostname,
                        ServerMap = s.CurrentMap.Alias,
                        ServerInfo = Stats.ServerStats[s.GetPort()],
                        Minimap = MinimapConfig.Read(@"Config\minimaps.cfg").MapInfo.Where(m => m.MapName == s.CurrentMap.Name),
                        MapKills = selectCount < 999 ? Stats.ServerStats[s.GetPort()].GetKillQueue().ToArray()
                                                        .Skip(Math.Min(Stats.MAX_KILLEVENTS - selectCount, Stats.ServerStats[s.GetPort()].GetKillQueue().Count - selectCount)) :
                                                        Stats.statLists.FirstOrDefault(x => x.Port == s.GetPort()).playerStats.GetKillsByMap(s.CurrentMap, selectCount)
                    })
                },
                additionalHeaders = new Dictionary<string, string>()
            };
            return resp;*/
        }
    }
}
