using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibraryCore.Dtos
{
    public class ProfileMeta : SharedInfo
    {
        public enum MetaType
        {
            Other,
            Information,
            AliasUpdate,
            ChatMessage,
            Penalized,
            ReceivedPenalty,
            QuickMessage
        }

        public DateTime When { get; set; }
        public string WhenString => Utilities.GetTimePassed(When, false);
        public string Key { get; set; }
        public dynamic Value { get; set; }
        public string Extra { get; set; }
        public virtual string Class => Value.GetType().ToString();
        public MetaType Type { get; set; }
        public int? Column { get; set; }
        public int? Order { get; set; }
    }
}
