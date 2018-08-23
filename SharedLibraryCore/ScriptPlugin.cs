using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibraryCore
{
    class ScriptPlugin : IPlugin
    {
        public string Name { get; set; }

        public float Version { get; set; }

        public string Author  {get;set;}

        private Jint.Engine ScriptEngine;
        private readonly string FileName;
        private IManager Manager;

        public ScriptPlugin(string fileName)
        {
            FileName = fileName;
            var watcher = new FileSystemWatcher()
            {
                Path = $"{Utilities.OperatingDirectory}Plugins{Path.DirectorySeparatorChar}",
                NotifyFilter = NotifyFilters.Size,
                Filter = fileName.Split(Path.DirectorySeparatorChar).Last()
            };

            watcher.Changed += Watcher_Changed;
            watcher.EnableRaisingEvents = true;
        }

        private async void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            try
            {
                await Initialize(Manager);
            }
            catch (Exception ex)
            {
                Manager.GetLogger().WriteError($"{Utilities.CurrentLocalization.LocalizationIndex["PLUGIN_IMPORTER_ERROR"]} {Name}");
                Manager.GetLogger().WriteDebug(ex.Message);
            }
        }

        public async Task Initialize(IManager mgr)
        {
            // it's been loaded before so we need to call the unload event
            if (ScriptEngine != null)
            {
                await OnUnloadAsync();
            }

            Manager = mgr;
            string script = File.ReadAllText(FileName);
            ScriptEngine = new Jint.Engine();

            ScriptEngine.Execute(script);
            ScriptEngine.SetValue("_localization", Utilities.CurrentLocalization);
            dynamic pluginObject = ScriptEngine.GetValue("plugin").ToObject();

            this.Author = pluginObject.author;
            this.Name = pluginObject.name;
            this.Version = (float)pluginObject.version;

            if (ScriptEngine != null)
            {
                await OnLoadAsync(mgr);
            }
        }

        public Task OnEventAsync(GameEvent E, Server S)
        {
            ScriptEngine.SetValue("_gameEvent", E);
            ScriptEngine.SetValue("_server", S);
            return Task.FromResult(ScriptEngine.Execute("plugin.onEventAsync(_gameEvent, _server)").GetCompletionValue());
        }

        public Task OnLoadAsync(IManager manager)
        {
            ScriptEngine.SetValue("_manager", manager);
            return Task.FromResult(ScriptEngine.Execute("plugin.onLoadAsync(_manager)").GetCompletionValue());
        }

        public Task OnTickAsync(Server S)
        {
            ScriptEngine.SetValue("_server", S);
            return Task.FromResult(ScriptEngine.Execute("plugin.onTickAsync(_server)").GetCompletionValue());
        }

        public Task OnUnloadAsync() => Task.FromResult(ScriptEngine.Execute("plugin.onUnloadAsync()").GetCompletionValue());
    }
}
