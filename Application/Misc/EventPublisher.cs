﻿using System;
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

                if (gameEvent.Type == GameEvent.EventType.Disconnect)
                {
                    OnClientDisconnect?.Invoke(this, gameEvent);
                }
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not publish event of type {EventType}", gameEvent.Type);
            }
        }
    }
}