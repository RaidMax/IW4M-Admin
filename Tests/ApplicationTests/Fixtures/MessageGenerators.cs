using IW4MAdmin.Plugins.Stats.Models;
using SharedLibraryCore.Database.Models;
using System;

namespace ApplicationTests.Fixtures
{
    public class MessageGenerators
    {
        public static EFClientMessage GenerateMessage(string content = null, DateTime? sent = null)
        {
            if (!sent.HasValue)
            {
                sent = DateTime.Now;
            }

            var rand = new Random();
            string endPoint = $"127.0.0.1:{rand.Next(1000, short.MaxValue)}";

            return new EFClientMessage()
            {
                Active = true,
                Message = content,
                TimeSent = sent.Value,
                Client = new EFClient()
                {
                    NetworkId = -1,
                    CurrentAlias = new EFAlias()
                    {
                        Name = "test"
                    }
                },
                Server = new EFServer()
                {
                    EndPoint = endPoint,
                    ServerId = long.Parse(endPoint.Replace(".", "").Replace(":", ""))
                }
            };
        }
    }
}
