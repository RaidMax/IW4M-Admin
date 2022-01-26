using Data.Models;

namespace SharedLibraryCore.Interfaces
{
    public interface IClientNoticeMessageFormatter
    {
        /// <summary>
        ///     builds a game formatted notice message
        /// </summary>
        /// <param name="currentPenalty">current penalty the message is for</param>
        /// <param name="originalPenalty">previous penalty the current penalty relates to</param>
        /// <param name="config">RCon parser config</param>
        /// <returns></returns>
        string BuildFormattedMessage(IRConParserConfiguration config, EFPenalty currentPenalty,
            EFPenalty originalPenalty = null);
    }
}