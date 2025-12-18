namespace WebfrontCore.Controllers.API.Models;

public class InfoResponse
{
    public int TotalConnectedClients { get; set; }
    public int TotalClientSlots { get; set; }
    public int TotalTrackedClients { get; set; }
    public required MetricSnapshot<int> TotalRecentClients { get; set; }

    public required MetricSnapshot<int?> MaxConcurrentClients { get; set; }
    public TimeSpan Uptime { get; set; }
}

public abstract class MetricSnapshot<T>
{
    public required T Value { get; set; }
    public DateTime? Time { get; set; }
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
}
