using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak;
using Kayak.Http;
using System.Net;

namespace Webfront_Plugin
{
    static class Manager
    {
        public static IScheduler webScheduler { get; private set; }
        public static Framework webFront { get; private set; }
        public static IPAddress lastIP;
        public static IServer webServer;

        public static void Init()
        {
            webScheduler = KayakScheduler.Factory.Create(new SchedulerDelegate());
            webServer = KayakServer.Factory.CreateHttp(new RequestDelegate(), webScheduler);
            webFront = new Framework();

            using (webServer.Listen(new IPEndPoint(IPAddress.Any, 1624)))
                webScheduler.Start();
        }      
    }

    class SchedulerDelegate : ISchedulerDelegate
    {
        public void OnException(IScheduler scheduler, Exception e)
        {
            
        }

        public void OnStop(IScheduler scheduler)
        {
            
        }
    }

    class RequestDelegate : IHttpRequestDelegate
    {
        public void OnRequest(HttpRequestHead request, IDataProducer requestBody, IHttpResponseDelegate response)
        {
            DefaultKayakServer castCrap = (DefaultKayakServer)Manager.webServer;
            Manager.lastIP = castCrap.clientAddress.Address;
        
            string body = Manager.webFront.processRequest(request);
            var headers = new HttpResponseHead()
                {
                    Status = "200 OK",
                    Headers = new Dictionary<string, string>() 
                    {
                        { "Content-Type", "text/html" },
                        { "Content-Length", body.Length.ToString() },
                    }
                };

            response.OnResponse(headers, new BufferedProducer(body));
        }
    }

    class BufferedProducer : IDataProducer
    {
        ArraySegment<byte> data;

        public BufferedProducer(string data) : this(data, Encoding.UTF8) { }
        public BufferedProducer(string data, Encoding encoding) : this(encoding.GetBytes(data)) { }
        public BufferedProducer(byte[] data) : this(new ArraySegment<byte>(data)) { }
        public BufferedProducer(ArraySegment<byte> data)
        {
            this.data = data;
        }

        public IDisposable Connect(IDataConsumer channel)
        {
            channel.OnData(data, null);
            channel.OnEnd();
            return null;
        }
    }

    class BufferedConsumer : IDataConsumer
    {
        List<ArraySegment<byte>> buffer = new List<ArraySegment<byte>>();
        Action<string> resultCallback;
        Action<Exception> errorCallback;

        public BufferedConsumer(Action<string> resultCallback, Action<Exception> errorCallback)
        {
            this.resultCallback = resultCallback;
            this.errorCallback = errorCallback;
        }

        public bool OnData(ArraySegment<byte> data, Action continuation)
        {
            buffer.Add(data);
            return false;
        }

        public void OnError(Exception error)
        {
            errorCallback(error);
        }

        public void OnEnd()
        {
            var str = buffer
                .Select(b => Encoding.UTF8.GetString(b.Array, b.Offset, b.Count))
                .Aggregate((result, next) => result + next);

            resultCallback(str);
        }
    }
}
