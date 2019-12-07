using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Linq;
using System.Text.RegularExpressions;

namespace SharedLibraryCore
{
    [HtmlTargetElement("color-code")]
    public class ColorCode : TagHelper
    {
        public string Value { get; set; }

        public bool Allow { get; set; } = false;

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "ColorCode";
            output.TagMode = TagMode.StartTagAndEndTag;

            if (Allow)
            {
                var matches = Regex.Matches(Value, @"\^([0-9]|\:)([^\^]*)");
                foreach (Match match in matches)
                {
                    char colorCode = match.Groups[1].ToString().Last();
                    output.PreContent.AppendHtml($"<span class='text-color-code-{(colorCode >= 48 && colorCode <= 57 ? colorCode.ToString() : ((int)colorCode).ToString())}'>");
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
