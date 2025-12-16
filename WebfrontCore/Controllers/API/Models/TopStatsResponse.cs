using IW4MAdmin.Plugins.Stats.Web.Dtos;

namespace WebfrontCore.Controllers.API.Dtos
{
    public class TopStatsResponse
    {
        public List<TopStatsInfo> Players { get; set; }
        public long TotalRankedClients { get; set; }
    }
}
