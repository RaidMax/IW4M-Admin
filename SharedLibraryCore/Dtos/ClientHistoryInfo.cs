using System;
using System.Collections.Generic;

namespace SharedLibraryCore.Dtos
{
    public class ClientHistoryInfo
    {
        public long ServerId { get; set; }
        public List<ClientCountSnapshot> ClientCounts { get; set; }
    }

    public class ClientCountSnapshot
    {
        public DateTime Time { get; set; }
        public string TimeString => Time.ToString("yyyy-MM-ddTHH:mm:ssZ");
        public int ClientCount { get; set; }
    }
}