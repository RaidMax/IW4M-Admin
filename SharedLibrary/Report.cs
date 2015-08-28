using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharedLibrary
{
    public class Report
    {
        public Report(Player T, Player O, String R)
        {
            Target = T;
            Origin = O;
            Reason = R;
        }

        public Player Target { get; private set; }
        public Player Origin { get; private set; }
        public String Reason { get; private set; }
    }
}
