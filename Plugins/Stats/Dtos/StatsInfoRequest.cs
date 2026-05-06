namespace Stats.Dtos
{
    public class StatsInfoRequest
    {
        /// <summary>
        /// client identifier
        /// </summary>
        public int? ClientId { get; set; }
        public string? ServerEndpoint { get; set; }

        /// <summary>
        /// Performance-bucket code filter. The setter lower-cases so the DB
        /// equality comparison can't silently miss on a capitalised input —
        /// <c>EFPerformanceBucket.Code</c> stores the canonical lower-cased
        /// form. Empty/whitespace stays null so "no filter" remains
        /// distinguishable from the explicit default bucket. Mirrors
        /// <c>Stats.Config.PerformanceBucketCodes.Normalize</c>.
        /// </summary>
        public string PerformanceBucketCode
        {
            get;
            init => field = string.IsNullOrWhiteSpace(value) ? null : value.ToLowerInvariant();
        }
    }
}
