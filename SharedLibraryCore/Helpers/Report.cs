using SharedLibraryCore.Database.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibraryCore.Helpers
{
    public class Report
    {
        public EFClient Target { get; set; }
        public EFClient Origin { get; set; }
        public string Reason { get;  set; }
    }
}
