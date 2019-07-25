using System.Threading.Tasks;
using RestEase;

namespace IW4MAdmin.Application.API.GameLogServer
{
    [Header("User-Agent", "IW4MAdmin-RestEase")]
    public interface IGameLogServer
    {
        [Get("log/{path}/{key}")]
        Task<LogInfo> Log([Path] string path, [Path] string key);
    }
}
