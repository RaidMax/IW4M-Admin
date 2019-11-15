using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IW4MAdmin.Application.Misc
{
    internal class EventPerformance
    {
        public long ExecutionTime { get; set; }
        public GameEvent Event { get; set; }
        public string EventInfo => $"{Event.Type}, {Event.FailReason}, {Event.IsBlocking}, {Event.Data}, {Event.Message}, {Event.Extra}";
    }

    public class DuplicateKeyComparer<TKey> : IComparer<TKey> where TKey : IComparable
    {
        public int Compare(TKey x, TKey y)
        {
            int result = x.CompareTo(y);

            if (result == 0)
                return 1;
            else
                return result;
        }
    }

    internal class EventProfiler
    {
        public double AverageEventTime { get; private set; }
        public double MaxEventTime => Events.Values.Last().ExecutionTime;
        public double MinEventTime => Events.Values[0].ExecutionTime;
        public int TotalEventCount => Events.Count;
        public SortedList<long, EventPerformance> Events { get; private set; } = new SortedList<long, EventPerformance>(new DuplicateKeyComparer<long>());
        private readonly ILogger _logger;

        public EventProfiler(ILogger logger)
        {
            _logger = logger;
        }

        public void Profile(DateTime start, DateTime end, GameEvent gameEvent)
        {
            _logger.WriteDebug($"Starting profile of event {gameEvent.Id}");
            long executionTime = (long)Math.Round((end - start).TotalMilliseconds);

            var perf = new EventPerformance()
            {
                Event = gameEvent,
                ExecutionTime = executionTime
            };

            lock (Events)
            {
                Events.Add(executionTime, perf);
            }

            AverageEventTime = (AverageEventTime * (TotalEventCount - 1) + executionTime) / TotalEventCount;
            _logger.WriteDebug($"Finished profile of event {gameEvent.Id}");
        }
    }
}
