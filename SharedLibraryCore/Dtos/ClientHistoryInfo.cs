using System;
using System.Collections.Generic;

namespace SharedLibraryCore.Dtos
{
    public class ClientHistoryInfo
    {
        public long ServerId { get; set; }
        public List<ClientCountSnapshot> ClientCounts { get; set; } = new();
    }

    public class ClientCountSnapshot
    {
        private const int UpdateInterval = 5;
        public DateTime Time { get; set; }
        public string TimeString => new DateTime(Time.Year, Time.Month, Time.Day, Time.Hour,
                Math.Min(59, UpdateInterval * (int)Math.Round(Time.Minute / (float)UpdateInterval)), 0)
            .ToString("yyyy-MM-ddTHH:mm:ssZ");
        public int ClientCount { get; set; }
        public bool ConnectionInterrupted { get;set; }
        public string Map { get; set; }
        public string MapAlias { get; set; }
    }
}
