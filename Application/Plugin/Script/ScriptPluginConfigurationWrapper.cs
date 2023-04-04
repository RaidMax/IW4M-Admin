using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using IW4MAdmin.Application.Configuration;
using Jint;
using Jint.Native;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Plugin.Script;

public class ScriptPluginConfigurationWrapper
{
    private readonly ScriptPluginConfiguration _config;
    private readonly IConfigurationHandlerV2<ScriptPluginConfiguration> _configHandler;
    private readonly Engine _scriptEngine;
    private string _pluginName;

    public ScriptPluginConfigurationWrapper(string pluginName, Engine scriptEngine, IConfigurationHandlerV2<ScriptPluginConfiguration> configHandler)
    {
        _pluginName = pluginName;
        _scriptEngine = scriptEngine;
        _configHandler = configHandler;
        _config = configHandler.Get("ScriptPluginSettings", new ScriptPluginConfiguration()).GetAwaiter().GetResult();
    }

    public void SetName(string name)
    {
        _pluginName = name;
    }

    public async Task SetValue(string key, object value)
    {
        var castValue = value;

        if (value is double doubleValue)
        {
            castValue = AsInteger(doubleValue) ?? value;
        }

        if (value is object[] array && array.All(item => item is double d && AsInteger(d) != null))
        {
            castValue = array.Select(item => AsInteger((double)item)).ToArray();
        }

        if (!_config.ContainsKey(_pluginName))
        {
            _config.Add(_pluginName, new Dictionary<string, object>());
        }

        var plugin = _config[_pluginName];

        if (plugin.ContainsKey(key))
        {
            plugin[key] = castValue;
        }

        else
        {
            plugin.Add(key, castValue);
        }

        await _configHandler.Set(_config);
    }

    public JsValue GetValue(string key)
    {
        if (!_config.ContainsKey(_pluginName))
        {
            return JsValue.Undefined;
        }

        if (!_config[_pluginName].ContainsKey(key))
        {
            return JsValue.Undefined;
        }

        var item = _config[_pluginName][key];

        if (item is JsonElement { ValueKind: JsonValueKind.Array } jElem)
        {
            item = jElem.Deserialize<List<dynamic>>();
        }

        return JsValue.FromObject(_scriptEngine, item);
    }
        
    private static int? AsInteger(double value)
    {
        return int.TryParse(value.ToString(CultureInfo.InvariantCulture), out var parsed) ? parsed : null;
    }
}
