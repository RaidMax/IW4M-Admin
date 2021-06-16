using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using IW4MAdmin.Application.Configuration;
using Jint;
using Jint.Native;
using Newtonsoft.Json.Linq;

namespace IW4MAdmin.Application.Misc
{
    public class ScriptPluginConfigurationWrapper
    {
        private readonly BaseConfigurationHandler<ScriptPluginConfiguration> _handler;
        private readonly ScriptPluginConfiguration _config;
        private readonly string _pluginName;
        private readonly Engine _scriptEngine;

        public ScriptPluginConfigurationWrapper(string pluginName, Engine scriptEngine)
        {
            _handler = new BaseConfigurationHandler<ScriptPluginConfiguration>("ScriptPluginSettings");
            _config = _handler.Configuration() ??
                      (ScriptPluginConfiguration) new ScriptPluginConfiguration().Generate();
            _pluginName = pluginName;
            _scriptEngine = scriptEngine;
        }

        private static int? AsInteger(double d)
        {
            return int.TryParse(d.ToString(CultureInfo.InvariantCulture), out var parsed) ? parsed : (int?) null;
        }

        public async Task SetValue(string key, object value)
        {
            var castValue = value;

            if (value is double d)
            {
                castValue = AsInteger(d) ?? value;
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

            _handler.Set(_config);
            await _handler.Save();
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

            if (item is JArray array)
            {
                item = array.ToObject<List<dynamic>>();
            }

            return JsValue.FromObject(_scriptEngine, item);
        }
    }
}