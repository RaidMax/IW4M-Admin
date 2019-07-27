using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebfrontCore.Controllers
{
    [Route("dynamic")]
    public class DynamicFileController : BaseController
    {
        private static readonly IDictionary<string, string> _fileCache = new Dictionary<string, string>();

        [Route("css/{fileName}")]
        public async Task<IActionResult> Css(string fileName)
        {
            if (fileName.EndsWith(".css"))
            {
                if (!_fileCache.ContainsKey(fileName))
                {
#if DEBUG
                    string path = $"X:\\IW4MAdmin\\WebfrontCore\\wwwroot\\css\\{fileName}";
#else
                    string path = $"wwwroot\\css\\{fileName}";
#endif
                    string cssData = await System.IO.File.ReadAllTextAsync(path);
                    cssData = await Manager.MiddlewareActionHandler.Execute(cssData, "custom_css_accent");
                    _fileCache.Add(fileName, cssData);
                }

                return Content(_fileCache[fileName], "text/css");
            }

            return StatusCode(400);
        }
    }
}
