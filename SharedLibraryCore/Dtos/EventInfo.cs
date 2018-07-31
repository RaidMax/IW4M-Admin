using System;
using static SharedLibraryCore.GameEvent;

namespace SharedLibraryCore.Dtos
{
    /// <summary>
    /// This class wraps the information related to a generated event for the API
    /// </summary>
    public class EventInfo
    {
        public EntityInfo GameInfo { get; set; }
        public EntityInfo OriginEntity { get; set; }
        public EntityInfo TargetEntity { get; set; }
        public EntityInfo EventType { get; set; }
        public EntityInfo OwnerEntity { get; set; }
        public DateTime EventTime { get; set; }
        public string ExtraInfo { get; set; }
        public string Id { get; private set; } = Guid.NewGuid().ToString();
    }
}