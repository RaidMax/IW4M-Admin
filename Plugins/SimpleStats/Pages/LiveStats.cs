using SharedLibrary;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatsPlugin.Pages
{
    public class LiveStats : HTMLPage
    {
        public LiveStats() : base(false) { }

        public override string GetContent(NameValueCollection querySet, IDictionary<string, string> headers)
        {
            StringBuilder S = new StringBuilder();
            S.Append(LoadHeader());

            IFile stats = new IFile("webfront\\stats.html");
            S.Append(stats.GetText());
            stats.Close();

            S.Append(LoadFooter());

            return S.ToString();
        }

        public override string GetName() => "Server Stats";
        public override string GetPath() => "/stats";
    }
}
