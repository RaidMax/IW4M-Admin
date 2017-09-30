using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharedLibrary
{
    public class WebService
    {
        public static List<IPage> PageList { get; set; }

        public static void Init()
        {
            PageList = new List<IPage>();
        }
    }

    public struct HttpResponse
    {
        public string contentType;
        public string content;
        public Dictionary<string, string> additionalHeaders;
    }

    public interface IPage
    {
        string GetPath();
        string GetName();
        HttpResponse GetPage(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers);
        bool Visible();
    }

    public abstract class HTMLPage : IPage
    {
        private bool visible;

        public HTMLPage()
        {
            visible = true;
        }

        public HTMLPage(bool visible)
        {
            this.visible = visible;
        }

        protected string GetContentType()
        {
            return "text/html";
        }

        protected string LoadFile(string filename)
        {
            string s;
            
            IFile HTML = new IFile(filename);
            s = HTML.GetText();
            HTML.Close();

            return s;
        }

        protected string LoadHeader()
        {
            return LoadFile("webfront\\header.html");
        }

        protected string LoadFooter()
        {
            return LoadFile("webfront\\footer.html");
        }

        public bool Visible()
        {
            return visible;
        }

        virtual public string GetPath()
        {
            return "";
        }

        abstract public string GetName();
        virtual public Dictionary<string, string> GetHeaders(IDictionary<string, string> requestHeaders)
        {
            return new Dictionary<string, string>();
        }
        abstract public string GetContent(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers);
        

        public HttpResponse GetPage(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            HttpResponse resp = new HttpResponse()
            {
                content = GetContent(querySet, headers),
                contentType = GetContentType(),
                additionalHeaders = GetHeaders(headers)
            };
            return resp;
        }
    }
}
