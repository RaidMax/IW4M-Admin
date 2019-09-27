namespace Stats.Config
{
    public class DistributionConfiguration
    {
        public enum DistributionType
        {
            Normal,
            LogNormal
        }

        public DistributionType Type { get; set; }
        public double Mean { get; set; }
        public double StandardDeviation { get; set; }
    }
}
