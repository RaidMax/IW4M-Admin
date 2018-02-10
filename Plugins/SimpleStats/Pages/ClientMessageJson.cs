using SharedLibrary;
using SharedLibrary.Database.Models;
using SharedLibrary.Services;
using StatsPlugin.Models;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatsPlugin.Pages
{
    public class ClientMessageJson : IPage
    {
        public string GetName() => "Client Chat JSON";
        public string GetPath() => "/_clientchat";
        public string GetContentType() => "application/json";
        public bool Visible() => false;

        public async  Task<HttpResponse> GetPage(NameValueCollection querySet, IDictionary<string, string> headers)
        {
            int clientId = Convert.ToInt32(querySet["clientid"]);
            var messageSvc = new GenericRepository<EFClientMessage>();
            var clientMessages = (await messageSvc.FindAsync(m => m.ClientId == clientId));
            
            HttpResponse resp = new HttpResponse()
            {
                contentType = GetContentType(),
                content = clientMessages.Select(c => new
                {
                    ClientID = c.ClientId,
                    ServerID = c.ServerId,
                     c.Message,
                     c.TimeSent,
                    ClientName = c.Client.Name,
                }),
                additionalHeaders = new Dictionary<string, string>()
            };

            return resp;
        }
    }
}
