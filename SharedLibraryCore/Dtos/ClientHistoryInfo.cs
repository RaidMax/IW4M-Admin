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
        public DateTime Time { get; set; }
        public string TimeString => Time.ToString("yyyy-MM-ddTHH:mm:ssZ");
        public int ClientCount { get; set; }
        public bool ConnectionInterrupted { get;set; }
        public string Map { get; set; }
        public string MapAlias { get; set; }
    }
}
