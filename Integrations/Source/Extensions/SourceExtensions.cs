using System.Text;

namespace Integrations.Source.Extensions
{
    public static class SourceExtensions
    {
        public static string ReplaceUnfriendlyCharacters(this string source)
        {
            var result = new StringBuilder();
            var quoteStart = false;
            var quoteIndex = 0;
            var index = 0;

            foreach (var character in source)
            {
                if (character == '%')
                {
                    result.Append('‰');
                }

                else if ((character == '"' || character == '\'') && index + 1 != source.Length)
                {
                    if (quoteIndex > 0)
                    {
                        result.Append(!quoteStart ? "«" : "»");
                        quoteStart = !quoteStart;
                    }

                    else
                    {
                        result.Append('"');
                    }
              
                    quoteIndex++;
                }

                else
                {
                    result.Append(character);
                }

                index++;
            }

            return result.ToString();
        }
    }
}