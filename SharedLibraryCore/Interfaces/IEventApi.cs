using SharedLibraryCore.Dtos;
using System.Collections.Generic;

namespace SharedLibraryCore.Interfaces
{
    public interface IEventApi
    {
        void OnServerEvent(object sender, GameEvent E);
        Queue<EventInfo> GetEvents();
    }
}
