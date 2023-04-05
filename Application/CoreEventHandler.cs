using System;
using System.Collections.Concurrent;
using SharedLibraryCore;
using SharedLibraryCore.Events;
using SharedLibraryCore.Interfaces;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Events.Server;
using SharedLibraryCore.Interfaces.Events;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application
{
    public class CoreEventHandler : ICoreEventHandler
    {
        private const int MaxCurrentEvents = 25;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _onProcessingEvents = new(MaxCurrentEvents, MaxCurrentEvents);
        private readonly ManualResetEventSlim _onEventReady = new(false);
        private readonly ConcurrentQueue<(IManager, CoreEvent)> _runningEventTasks = new();
        private CancellationToken _cancellationToken;
        private int _activeTasks;

        private static readonly GameEvent.EventType[] OverrideEvents =
        {
            GameEvent.EventType.Connect,
            GameEvent.EventType.Disconnect,
            GameEvent.EventType.Quit,
            GameEvent.EventType.Stop
        };

        public CoreEventHandler(ILogger<CoreEventHandler> logger)
        {
            _logger = logger;
        }

        public void QueueEvent(IManager manager, CoreEvent coreEvent)
        {
            _runningEventTasks.Enqueue((manager, coreEvent));
            _onEventReady.Set();
        }

        public void StartProcessing(CancellationToken token)
        {
            _cancellationToken = token;

            while (!_cancellationToken.IsCancellationRequested)
            {
                _onEventReady.Reset();
                
                try
                {
                    _onProcessingEvents.Wait(_cancellationToken);

                    if (!_runningEventTasks.TryDequeue(out var coreEvent))
                    {
                        if (_onProcessingEvents.CurrentCount < MaxCurrentEvents)
                        {
                            _onProcessingEvents.Release(1);
                        }
                        
                        _onEventReady.Wait(_cancellationToken);
                        continue;
                    }
                    
                    _logger.LogDebug("Start processing event {Name} {SemaphoreCount} - {QueuedTasks}",
                        coreEvent.Item2.GetType().Name, _onProcessingEvents.CurrentCount, _runningEventTasks.Count);

                    _ = Task.Factory.StartNew(() =>
                    {
                        Interlocked.Increment(ref _activeTasks);
                        _logger.LogDebug("[Start] Active Tasks = {TaskCount}", _activeTasks);
                        return HandleEventTaskExecute(coreEvent);
                    });
                }
                catch (OperationCanceledException)
                {
                    // ignored
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not enqueue event for processing");
                }
            }
        }

        private async Task HandleEventTaskExecute((IManager, CoreEvent) coreEvent)
        {
            try
            {
                await GetEventTask(coreEvent.Item1, coreEvent.Item2);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Event timed out {Type}", coreEvent.Item2.GetType().Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not complete invoke for {EventType}",
                    coreEvent.Item2.GetType().Name);
            }
            finally
            {
                if (_onProcessingEvents.CurrentCount < MaxCurrentEvents)
                {
                    _logger.LogDebug("Freeing up event semaphore for next event {SemaphoreCount}",
                        _onProcessingEvents.CurrentCount);
                    _onProcessingEvents.Release(1);
                }

                Interlocked.Decrement(ref _activeTasks);
                _logger.LogDebug("[Complete] {Type}, Active Tasks = {TaskCount} - {Queue}", coreEvent.Item2.GetType(),
                    _activeTasks, _runningEventTasks.Count);
            }
        }

        private Task GetEventTask(IManager manager, CoreEvent coreEvent)
        {
            return coreEvent switch
            {
                GameEvent gameEvent => BuildLegacyEventTask(manager, coreEvent, gameEvent),
                GameServerEvent gameServerEvent => IGameServerEventSubscriptions.InvokeEventAsync(gameServerEvent,
                    manager.CancellationToken),
                ManagementEvent managementEvent => IManagementEventSubscriptions.InvokeEventAsync(managementEvent,
                    manager.CancellationToken),
                _ => Task.CompletedTask
            };
        }

        private async Task BuildLegacyEventTask(IManager manager, CoreEvent coreEvent, GameEvent gameEvent)
        {
            if (manager.IsRunning || OverrideEvents.Contains(gameEvent.Type))
            {
                await manager.ExecuteEvent(gameEvent);
                await IGameEventSubscriptions.InvokeEventAsync(coreEvent, manager.CancellationToken);
                return;
            }
            
            _logger.LogDebug("Skipping event as we're shutting down {EventId}", gameEvent.IncrementalId);
        }
    }
}
