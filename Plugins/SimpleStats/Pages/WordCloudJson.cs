using SharedLibrary;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatsPlugin.Pages
{

    public class WordCloudJson : IPage
    {
        public string GetName() => "Word Cloud JSON";
        public string GetPath() => "/_words";
        public string GetContentType() => "application/json";
        public bool Visible() => false;

        public async Task<HttpResponse> GetPage(NameValueCollection querySet, IDictionary<string, string> headers)
        {
            // todo: this
            HttpResponse resp = new HttpResponse()
            {
                contentType = GetContentType(),
                content = null,
                additionalHeaders = new Dictionary<string, string>()
            };

            return resp;
        }
    }
}
