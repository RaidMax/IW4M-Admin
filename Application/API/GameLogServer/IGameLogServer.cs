using System.Threading.Tasks;
using Refit;

namespace IW4MAdmin.Application.API.GameLogServer
{
    [Headers("User-Agent: IW4MAdmin-RestEase")]
    public interface IGameLogServer
    {
        [Get("/log/{path}/{key}")]
        Task<LogInfo> Log(string path, string key);
    }
}
