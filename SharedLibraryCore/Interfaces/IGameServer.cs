using System.Threading.Tasks;
using Data.Models;
using SharedLibraryCore.Database.Models;

namespace SharedLibraryCore.Interfaces
{
    public interface IGameServer
    {
        /// <summary>
        ///     kicks target on behalf of origin for given reason
        /// </summary>
        /// <param name="reason">reason client is being kicked</param>
        /// <param name="target">client to kick</param>
        /// <param name="origin">source of kick action</param>
        /// <param name="previousPenalty">previous penalty the kick is occuring for (if applicable)</param>
        /// <returns></returns>
        public Task Kick(string reason, EFClient target, EFClient origin, EFPenalty previousPenalty = null);
    }
}