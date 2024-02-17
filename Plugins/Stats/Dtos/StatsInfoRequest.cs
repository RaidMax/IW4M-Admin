namespace Stats.Dtos
{
    public class StatsInfoRequest
    {
        /// <summary>
        /// client identifier
        /// </summary>
        public int? ClientId { get; set; }
        public string ServerEndpoint { get; set; }
        public string PerformanceBucket { get; set; }
    }
}
