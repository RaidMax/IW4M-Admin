using IW4MAdmin.Application.Misc;
using SharedLibraryCore;
using SharedLibraryCore.Events;
using SharedLibraryCore.Interfaces;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application
{
    public class GameEventHandler : IEventHandler
    {
        private readonly EventLog _eventLog;
        private readonly ILogger _logger;
        private static readonly GameEvent.EventType[] overrideEvents = new[]
        {
            GameEvent.EventType.Connect,
            GameEvent.EventType.Disconnect,
            GameEvent.EventType.Quit,
            GameEvent.EventType.Stop
        };

        public GameEventHandler(ILogger<GameEventHandler> logger)
        {
            _eventLog = new EventLog();
            _logger = logger;
        }

        public void HandleEvent(IManager manager, GameEvent gameEvent)
        {
            if (manager.IsRunning || overrideEvents.Contains(gameEvent.Type))
            {
                EventApi.OnGameEvent(gameEvent);
                Task.Factory.StartNew(() => manager.ExecuteEvent(gameEvent));
            }
            else
            {
                _logger.LogDebug("Skipping event as we're shutting down {eventId}", gameEvent.Id);
            }
        }
    }
}
