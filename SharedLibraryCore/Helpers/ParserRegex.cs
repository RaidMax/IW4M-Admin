using System;
using System.Collections.Generic;
using System.Text;

namespace SharedLibraryCore.Interfaces
{
    public sealed class ParserRegex
    {
        public enum GroupType
        {
            EventType,
            OriginNetworkId,
            TargetNetworkId,
            OriginClientNumber,
            TargetClientNumber,
            OriginName,
            TargetName,
            OriginTeam,
            TargetTeam,
            Weapon,
            Damage,
            MeansOfDeath,
            HitLocation,
            Message,
            RConClientNumber = 100,
            RConScore = 101,
            RConPing = 102,
            RConNetworkId = 103,
            RConName = 104,
            RConIpAddress = 105,
            AdditionalGroup = 200
        }
        public string Pattern { get; set; }
        public Dictionary<GroupType, int> GroupMapping { get; private set; }

        public ParserRegex()
        {
            GroupMapping = new Dictionary<GroupType, int>();
        }
    }
}
