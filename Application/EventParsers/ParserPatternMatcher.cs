using IW4MAdmin.Application.Misc;
using SharedLibraryCore.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace IW4MAdmin.Application.EventParsers
{
    /// <summary>
    /// implementation of the IParserPatternMatcher for windows (really it's the only implementation)
    /// </summary>
    public class ParserPatternMatcher : IParserPatternMatcher
    {
        private Regex regex;

        /// <inheritdoc/>
        public void Compile(string pattern)
        {
            regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        /// <inheritdoc/>
        public IMatchResult Match(string input)
        {
            var match = regex.Match(input);

            return new ParserMatchResult()
            {
                Success = match.Success,
                Values = (match.Groups as IEnumerable<object>)?
                    .Select(_item => _item.ToString()).ToArray() ?? new string[0]
            };
        }
    }
}
