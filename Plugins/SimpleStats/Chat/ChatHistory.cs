using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatsPlugin
{
    public class ChatHistory
    {
        public int ClientID { get; set; }
        public string Message { get; set; }
        public int ServerID { get; set; }
        public DateTime TimeSent { get; set; }
    }
}
