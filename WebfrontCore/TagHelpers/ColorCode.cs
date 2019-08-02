using Microsoft.AspNetCore.Razor.TagHelpers;
using SharedLibraryCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WebfrontCore.TagHelpers
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
                string updated = Value;

                foreach (Match match in Regex.Matches(Value, @"\^([0-9]|\:)([^\^]*)"))
                {
                    char colorCode = match.Groups[1].ToString().Last();
                    updated = updated.Replace(match.Value, $"<span class='text-color-code-{(colorCode >= 48 && colorCode <= 57 ? colorCode.ToString() : ((int)colorCode).ToString())}'>{match.Groups[2].ToString()}</span>");
                }

                output.PreContent.SetHtmlContent(updated);
            }

            else
            {
                output.PreContent.SetHtmlContent(Value.StripColors());
            }
        }
    }
}
