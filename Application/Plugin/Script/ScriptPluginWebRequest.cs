using System.Collections.Generic;

namespace IW4MAdmin.Application.Plugin.Script;

public record ScriptPluginWebRequest(string Url, object Body = null, string Method = "GET", string ContentType = "text/plain",
    Dictionary<string, string> Headers = null);
