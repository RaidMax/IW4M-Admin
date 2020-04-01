using static SharedLibraryCore.GameEvent;
using static SharedLibraryCore.Server;

namespace ApplicationTests.Fixtures
{
    class LogEvent
    {
        public Game Game { get; set; }
        public string EventLine { get; set; }
        public EventType ExpectedEventType { get; set; }
        public string ExpectedData { get; set; }
        public string ExpectedMessage { get; set; }
        public string ExpectedOriginNetworkId { get; set; }
        public int? ExpectedOriginClientNumber { get; set; }
        public string ExpectedOriginClientName { get; set; }
        public string ExpectedTargetNetworkId { get; set; }
        public int? ExpectedTargetClientNumber { get; set; }
        public string ExpectedTargetClientName { get; set; }
        public int? ExpectedTime { get; set; }
    }

    class EventLogTest
    {
        public LogEvent[] Events { get; set; }
    }
}
