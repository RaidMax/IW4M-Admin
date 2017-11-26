using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SharedLibrary;
using System.Collections.Specialized;

namespace StatsPlugin.Chat
{
    public class ChatPage : HTMLPage
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

    public class WordCloudJSON : IPage
    {
        public string GetName() => "Word Cloud JSON";
        public string GetPath() => "/_words";
        public string GetContentType() => "application/json";
        public bool Visible() => false;

        public HttpResponse GetPage(NameValueCollection querySet, IDictionary<string, string> headers)
        {

            HttpResponse resp = new HttpResponse()
            {
                contentType = GetContentType(),
                content = Stats.ChatDB.GetWords().Select(w => new
                {
                    Word = w.Key,
                    Count = w.Value
                })
                .OrderByDescending(x => x.Count)
                .ToArray(),

                additionalHeaders = new Dictionary<string, string>()
            };

            return resp;
        }
    }

    public class ClientChatJSON : IPage
    {
        public string GetName() => "Client Chat JSON";
        public string GetPath() => "/_clientchat";
        public string GetContentType() => "application/json";
        public bool Visible() => false;

        public HttpResponse GetPage(NameValueCollection querySet, IDictionary<string, string> headers)
        {
            int clientID = Convert.ToInt32(querySet["clientid"]);
            var name = Stats.ManagerInstance.GetDatabase().GetClient(clientID).Name;

            HttpResponse resp = new HttpResponse()
            {
                contentType = GetContentType(),
                content = Stats.ChatDB.GetChatForPlayer(clientID).ToArray().Select(c => new
                {
                    ClientID = c.ClientID,
                    ServerID = c.ServerID,
                    Message = c.Message,
                    TimeSent = c.TimeSent,
                    ClientName = name,
                }),
                additionalHeaders = new Dictionary<string, string>()
            };

            return resp;
        }
    }
}
