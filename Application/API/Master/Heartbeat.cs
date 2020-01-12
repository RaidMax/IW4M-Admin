using System;
using System.Linq;
using System.Threading.Tasks;
using RestEase;

namespace IW4MAdmin.Application.API.Master
{
    /// <summary>
    /// Defines the heartbeat functionality for IW4MAdmin
    /// </summary>
    public class Heartbeat
    {
        /// <summary>
        /// Sends heartbeat to master server
        /// </summary>
        /// <param name="mgr"></param>
        /// <param name="firstHeartbeat"></param>
        /// <returns></returns>
        public static async Task Send(ApplicationManager mgr, bool firstHeartbeat = false)
        {
            var api = Endpoint.Get();

            if (firstHeartbeat)
            {
                var token = await api.Authenticate(new AuthenticationId()
                {
                    Id = mgr.GetApplicationSettings().Configuration().Id
                });

                api.AuthorizationToken = $"Bearer {token.AccessToken}";
            }

            var instance = new ApiInstance()
            {
                Id = mgr.GetApplicationSettings().Configuration().Id,
                Uptime = (int)(DateTime.UtcNow - mgr.StartTime).TotalSeconds,
                Version = Program.Version,
                Servers = mgr.Servers.Select(s =>
                            new ApiServer()
                            {
                                ClientNum = s.ClientNum,
                                Game = s.GameName.ToString(),
                                Version = s.Version,
                                Gametype = s.Gametype,
                                Hostname = s.Hostname,
                                Map = s.CurrentMap.Name,
                                MaxClientNum = s.MaxClients,
                                Id = s.EndPoint,
                                Port = (short)s.Port,
                                IPAddress = s.IP
                            }).ToList()
            };

            Response<ResultMessage> response = null;

            if (firstHeartbeat)
            {
                response = await api.AddInstance(instance);
            }

            else
            {
                response = await api.UpdateInstance(instance.Id, instance);
            }

            if (response.ResponseMessage.StatusCode != System.Net.HttpStatusCode.OK)
            {
                mgr.Logger.WriteWarning($"Response code from master is {response.ResponseMessage.StatusCode}, message is {response.StringContent}");
            }
        }
    }
}
