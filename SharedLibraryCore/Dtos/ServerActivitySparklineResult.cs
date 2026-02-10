namespace SharedLibraryCore.Dtos;

public class ServerActivitySparklineResult
{
    public double[] DailyPlayTimeMinutes { get; set; } = Array.Empty<double>();
    public long TotalPlaytimeMinutes { get; set; }
}
