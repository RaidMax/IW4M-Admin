using System;
using System.Collections.Generic;

namespace SharedLibraryCore.Interfaces
{
    public sealed class ParserRegex
    {
        /// <summary>
        ///     represents the logical mapping of information provided by
        ///     game logs, get status, and get dvar information
        /// </summary>
        public enum GroupType
        {
            EventType = 0,
            OriginNetworkId = 1,
            TargetNetworkId = 2,
            OriginClientNumber = 3,
            TargetClientNumber = 4,
            OriginName = 5,
            TargetName = 6,
            OriginTeam = 7,
            TargetTeam = 8,
            Weapon = 9,
            Damage = 10,
            MeansOfDeath = 11,
            HitLocation = 12,
            Message = 13,
            RConClientNumber = 100,
            RConScore = 101,
            RConPing = 102,
            RConNetworkId = 103,
            RConName = 104,
            RConIpAddress = 105,
            RConDvarName = 106,
            RConDvarValue = 107,
            RConDvarDefaultValue = 108,
            RConDvarLatchedValue = 109,
            RConDvarDomain = 110,
            RConStatusMap = 111,
            RConStatusGametype = 112,
            RConStatusHostname = 113,
            RConStatusMaxPlayers = 114,
            AdditionalGroup = 200
        }

        private string pattern;

        public ParserRegex(IParserPatternMatcher pattern)
        {
            GroupMapping = new Dictionary<GroupType, int>();
            PatternMatcher = pattern;
        }

        public IParserPatternMatcher PatternMatcher { get; }

        /// <summary>
        ///     stores the regular expression groups that will be mapped to group types
        /// </summary>
        public string Pattern
        {
            get => pattern;
            set
            {
                pattern = value;
                PatternMatcher.Compile(value);
            }
        }

        /// <summary>
        ///     stores the mapping from group type to group index in the regular expression
        /// </summary>
        public Dictionary<GroupType, int> GroupMapping { get; }

        /// <summary>
        ///     helper method to enable script parsers to app regex mapping
        ///     the first parameter specifies the group type contained in the regex pattern
        ///     the second parameter specifies the group index to retrieve in the matched regex pattern
        /// </summary>
        /// <param name="mapKey">group type</param>
        /// <param name="mapValue">group index</param>
        public void AddMapping(object mapKey, object mapValue)
        {
            if (int.TryParse(mapKey.ToString(), out var key) && int.TryParse(mapValue.ToString(), out var value))
            {
                if (GroupMapping.ContainsKey((GroupType)key))
                {
                    GroupMapping[(GroupType)key] = value;
                }

                else
                {
                    GroupMapping.Add((GroupType)key, value);
                }
            }

            if (mapKey.GetType() == typeof(GroupType) && mapValue.GetType().ToString() == "System.Int32")
            {
                var k = (GroupType)Enum.Parse(typeof(GroupType), mapKey.ToString());
                var v = int.Parse(mapValue.ToString());

                if (GroupMapping.ContainsKey(k))
                {
                    GroupMapping[k] = v;
                }

                else
                {
                    GroupMapping.Add(k, v);
                }
            }
        }
    }
}