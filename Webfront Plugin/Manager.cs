using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak;
using Kayak.Http;
using System.Net;

namespace Webfront_Plugin
{
    class Manager
    {
        public IScheduler webScheduler { get; private set; }
        public static Framework webFront { get; private set; }

        public Manager()
        {

        }

        public void Init()
        {
            webScheduler = KayakScheduler.Factory.Create(new SchedulerDelegate());
            var server = KayakServer.Factory.CreateHttp(new RequestDelegate(), webScheduler);
            webFront = new Framework();

            using (server.Listen(new IPEndPoint(IPAddress.Any, 1624)))
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
            /*if (request.Method.ToUpperInvariant() == "POST" && request.Uri.StartsWith("/bufferedecho"))
            {
                // when you subecribe to the request body before calling OnResponse,
                // the server will automatically send 100-continue if the client is 
                // expecting it.
                requestBody.Connect(new BufferedConsumer(bufferedBody =>
                {
                    var headers = new HttpResponseHead()
                    {
                        Status = "200 OK",
                        Headers = new Dictionary<string, string>() 
                            {
                                { "Content-Type", "text/plain" },
                                { "Content-Length", request.Headers["Content-Length"] },
                                { "Connection", "close" }
                            }
                    };
                    response.OnResponse(headers, new BufferedProducer(bufferedBody));
                }, error =>
                {
                    // XXX
                    // uh oh, what happens?
                }));
            }
            else if (request.Method.ToUpperInvariant() == "POST" && request.Uri.StartsWith("/echo"))
            {
                var headers = new HttpResponseHead()
                {
                    Status = "200 OK",
                    Headers = new Dictionary<string, string>() 
                    {
                        { "Content-Type", "text/plain" },
                        { "Connection", "close" }
                    }
                };
                if (request.Headers.ContainsKey("Content-Length"))
                    headers.Headers["Content-Length"] = request.Headers["Content-Length"];

                // if you call OnResponse before subscribing to the request body,
                // 100-continue will not be sent before the response is sent.
                // per rfc2616 this response must have a 'final' status code,
                // but the server does not enforce it.
                response.OnResponse(headers, requestBody);
            }*/

          
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
       
            /*
            if (request.Uri.StartsWith("/"))
            {
                var body = string.Format(
                    "Hello world.\r\nHello.\r\n\r\nUri: {0}\r\nPath: {1}\r\nQuery:{2}\r\nFragment: {3}\r\n",
                    request.Uri,
                    request.Path,
                    request.QueryString,
                    request.Fragment);

                var headers = new HttpResponseHead()
                {
                    Status = "200 OK",
                    Headers = new Dictionary<string, string>() 
                    {
                        { "Content-Type", "text/plain" },
                        { "Content-Length", body.Length.ToString() },
                    }
                };
                response.OnResponse(headers, new BufferedProducer(body));
            }
            else
            {
                var responseBody = "The resource you requested ('" + request.Uri + "') could not be found.";
                var headers = new HttpResponseHead()
                {
                    Status = "404 Not Found",
                    Headers = new Dictionary<string, string>()
                    {
                        { "Content-Type", "text/plain" },
                        { "Content-Length", responseBody.Length.ToString() }
                    }
                };
                var body = new BufferedProducer(responseBody);

                response.OnResponse(headers, body);
            }*/
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
