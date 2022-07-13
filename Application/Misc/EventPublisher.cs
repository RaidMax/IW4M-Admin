using System;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.Misc
{
    public class EventPublisher : IEventPublisher
    {
        public event EventHandler<GameEvent> OnClientDisconnect;
        public event EventHandler<GameEvent> OnClientConnect;
        public event EventHandler<GameEvent> OnClientMetaUpdated;

        private readonly ILogger _logger;

        public EventPublisher(ILogger<EventPublisher> logger)
        {
            _logger = logger;
        }

        public void Publish(GameEvent gameEvent)
        {
            _logger.LogDebug("Handling publishing event of type {EventType}", gameEvent.Type);

            try
            {
                if (gameEvent.Type == GameEvent.EventType.Connect)
                {
                    OnClientConnect?.Invoke(this, gameEvent);
                }

                if (gameEvent.Type == GameEvent.EventType.Disconnect && gameEvent.Origin.ClientId != 0)
                {
                    OnClientDisconnect?.Invoke(this, gameEvent);
                }

                if (gameEvent.Type == GameEvent.EventType.MetaUpdated)
                {
                    OnClientMetaUpdated?.Invoke(this, gameEvent);
                }
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not publish event of type {EventType}", gameEvent.Type);
            }
        }
    }
}
