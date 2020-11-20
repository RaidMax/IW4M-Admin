using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Misc
{
    /// <summary>
    /// implementation of IClientNoticeMessageFormatter
    /// </summary>
    public class ClientNoticeMessageFormatter : IClientNoticeMessageFormatter
    {
        private readonly ITranslationLookup _transLookup;
        private readonly ApplicationConfiguration _appConfig;

        public ClientNoticeMessageFormatter(ITranslationLookup transLookup, ApplicationConfiguration appConfig)
        {
            _transLookup = transLookup;
            _appConfig = appConfig;
        }

        public string BuildFormattedMessage(IRConParserConfiguration config, EFPenalty currentPenalty, EFPenalty originalPenalty = null)
        {
            var isNewLineSeparator = config.NoticeLineSeparator == Environment.NewLine;
            var penalty = originalPenalty ?? currentPenalty;
            var builder = new StringBuilder();
            // build the top level header
            var header = _transLookup[$"SERVER_{penalty.Type.ToString().ToUpper()}_TEXT"];
            builder.Append(header);
            builder.Append(config.NoticeLineSeparator);
            // build the reason
            var reason = _transLookup["GAME_MESSAGE_PENALTY_REASON"].FormatExt(penalty.Offense);

            if (isNewLineSeparator)
            {
                foreach (var splitReason in SplitOverMaxLength(reason, config.NoticeMaxCharactersPerLine))
                {
                    builder.Append(splitReason);
                    builder.Append(config.NoticeLineSeparator);
                }
            }

            else
            {
                builder.Append(reason);
                builder.Append(config.NoticeLineSeparator);
            }

            if (penalty.Type == EFPenalty.PenaltyType.TempBan)
            {
                // build the time remaining if temporary
                var timeRemainingValue = penalty.Expires.HasValue
                    ? (penalty.Expires - DateTime.UtcNow).Value.HumanizeForCurrentCulture()
                    : "--";
                var timeRemaining = _transLookup["GAME_MESSAGE_PENALTY_TIME_REMAINING"].FormatExt(timeRemainingValue);

                if (isNewLineSeparator)
                {
                    foreach (var splitReason in SplitOverMaxLength(timeRemaining, config.NoticeMaxCharactersPerLine))
                    {
                        builder.Append(splitReason);
                        builder.Append(config.NoticeLineSeparator);
                    }
                }

                else
                {
                    builder.Append(timeRemaining);
                }
            }

            if (penalty.Type == EFPenalty.PenaltyType.Ban)
            {
                // provide a place to appeal the ban (should always be specified but including a placeholder just incase)
                builder.Append(_transLookup["GAME_MESSAGE_PENALTY_APPEAL"].FormatExt(_appConfig.ContactUri ?? "--"));
            }
            
            // final format looks something like:
            /*
             * You are permanently banned
             * Reason - toxic behavior
             * Visit example.com to appeal
             */

            return builder.ToString();
        }

        private static IEnumerable<string> SplitOverMaxLength(string source, int maxCharactersPerLine)
        {
            if (source.Length <= maxCharactersPerLine)
            {
                return new[] {source};
            }

            var segments = new List<string>();
            var currentLocation = 0;
            while (currentLocation < source.Length)
            {
                var nextLocation = currentLocation + maxCharactersPerLine;
                // there's probably a more efficient way to do this but this is readable
                segments.Add(string.Concat(
                    source
                    .Skip(currentLocation)
                    .Take(Math.Min(maxCharactersPerLine, source.Length - currentLocation))));
                currentLocation = nextLocation;
            }

            if (currentLocation < source.Length)
            {
                segments.Add(source.Substring(currentLocation, source.Length - currentLocation));
            }

            return segments;
        }
    }
}