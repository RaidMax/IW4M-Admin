using SharedLibrary;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatsPlugin.Pages
{
    public class ClientMessages : HTMLPage
    {
        public override string GetContent(NameValueCollection querySet, IDictionary<string, string> headers)
        {
            StringBuilder S = new StringBuilder();
            S.Append(LoadHeader());

            IFile chat = new IFile("webfront\\chat.html");
            S.Append(chat.GetText());
            chat.Close();

            S.Append(LoadFooter());

            return S.ToString();
        }

        public override string GetName() => "Word Cloud";
        public override string GetPath() => "/chat";
    }
}
