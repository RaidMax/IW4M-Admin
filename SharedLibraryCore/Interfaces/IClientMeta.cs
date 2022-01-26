using System;

namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    ///     describes all the base attributes of a client meta object
    /// </summary>
    public interface IClientMeta
    {
        MetaType Type { get; }
        DateTime When { get; }

        bool IsSensitive { get; }
        bool ShouldDisplay { get; }

        // sorting purposes
        public int? Column { get; set; }
        public int? Order { get; set; }
    }

    public enum MetaType
    {
        Other,
        Information,
        AliasUpdate,
        ChatMessage,
        Penalized,
        ReceivedPenalty,
        QuickMessage,
        ConnectionHistory
    }
}