using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharedLibrary
{
    public class WebService
    {
        public static List<IPage> pageList { get; private set; }

        public static void Init()
        {
            pageList = new List<IPage>();
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
        string getPath();
        string getName();
        HttpResponse getPage(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers);
        bool isVisible();
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

        protected string getContentType()
        {
            return "text/html";
        }

        protected string loadFile(string filename)
        {
            string s;
            
            IFile HTML = new IFile(filename);
            s = HTML.getLines();
            HTML.Close();

            return s;
        }

        protected string loadHeader()
        {
            return loadFile("webfront\\header.html");
        }

        protected string loadFooter()
        {
            return loadFile("webfront\\footer.html");
        }

        public bool isVisible()
        {
            return visible;
        }

        virtual public string getPath()
        {
            return "";
        }

        abstract public string getName();
        virtual public Dictionary<string, string> getHeaders(IDictionary<string, string> requestHeaders)
        {
            return new Dictionary<string, string>();
        }
        abstract public string getContent(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers);
        

        public HttpResponse getPage(System.Collections.Specialized.NameValueCollection querySet, IDictionary<string, string> headers)
        {
            HttpResponse resp = new HttpResponse()
            {
                content = getContent(querySet, headers),
                contentType = getContentType(),
                additionalHeaders = getHeaders(headers)
            };
            return resp;
        }
    }
}
