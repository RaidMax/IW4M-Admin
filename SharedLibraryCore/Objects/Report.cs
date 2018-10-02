using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibraryCore.Objects
{
    public class Report
    {
        public Player Target { get; set; }
        public Player Origin { get; set; }
        public String Reason { get;  set; }
    }
}
