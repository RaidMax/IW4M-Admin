namespace SharedLibraryCore.Dtos;

public class ServerActivitySparklineResult
{
    public List<double> DailyPlayTimeMinutes { get; set; } = new();
    public long TotalPlaytimeMinutes { get; set; }
}
