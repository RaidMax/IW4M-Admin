namespace WebfrontCore.Components.Features.Home.Models;

public class SystemInfo
{
     public int TotalConnectedClients { get; set; }
     public int TotalClientSlots { get; set; }
     public int TotalTrackedClients { get; set; }
     public MetricSnapshot<int> TotalRecentClients { get; set; }

     public MetricSnapshot<int?> MaxConcurrentClients { get; set; }
     public TimeSpan Uptime { get; set; }
     
     public class MetricSnapshot<T>
     {
         public T Value { get; set; }
         public DateTime? Time { get; set; }
         public DateTime? StartAt { get; set; }
         public DateTime? EndAt { get; set; }
     }
}
