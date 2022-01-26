using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Razor.TagHelpers;
using SharedLibraryCore.Configuration;

namespace SharedLibraryCore
{
    [HtmlTargetElement("color-code")]
    public class ColorCode : TagHelper
    {
        private readonly bool _allow;

        public ColorCode(ApplicationConfiguration appConfig)
        {
            _allow = appConfig?.EnableColorCodes ?? false;
        }

        public string Value { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "ColorCode";
            output.TagMode = TagMode.StartTagAndEndTag;

            if (_allow)
            {
                var matches = Regex.Matches(Value, @"\^([0-9]|\:)([^\^]*)");
                foreach (Match match in matches)
                {
                    var colorCode = match.Groups[1].ToString().Last();
                    output.PreContent.AppendHtml(
                        $"<span class='text-color-code-{(colorCode >= 48 && colorCode <= 57 ? colorCode.ToString() : ((int)colorCode).ToString())}'>");
                    output.PreContent.Append(match.Groups[2].ToString());
                    output.PreContent.AppendHtml("</span>");
                }

                if (matches.Count <= 1)
                {
                    output.PreContent.SetContent(Value.StripColors());
                }
            }

            else
            {
                output.PreContent.SetContent(Value.StripColors());
            }
        }
    }
}