using Jint;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharedLibraryCore
{
    public class ScriptPlugin : IPlugin
    {
        public string Name { get; set; }

        public float Version { get; set; }

        public string Author { get; set; }

        private Engine ScriptEngine;
        private readonly string FileName;
        private IManager Manager;
        private readonly FileSystemWatcher _watcher;
        private readonly SemaphoreSlim _fileChanging;
        private bool successfullyLoaded;

        public ScriptPlugin(string fileName)
        {
            FileName = fileName;
            _fileChanging = new SemaphoreSlim(1, 1);
            _watcher = new FileSystemWatcher()
            {
                Path = $"{Utilities.OperatingDirectory}Plugins{Path.DirectorySeparatorChar}",
                NotifyFilter = NotifyFilters.Size,
                Filter = fileName.Split(Path.DirectorySeparatorChar).Last()
            };

            _watcher.Changed += Watcher_Changed;
            _watcher.EnableRaisingEvents = true;
        }

        ~ScriptPlugin()
        {
            _watcher.Dispose();
            _fileChanging.Dispose();
        }

        private async void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            try
            {
                await _fileChanging.WaitAsync();
                await Initialize(Manager);
            }

            catch (Exception ex)
            {
                Manager.GetLogger(0).WriteError(Utilities.CurrentLocalization.LocalizationIndex["PLUGIN_IMPORTER_ERROR"].FormatExt(Name));
                Manager.GetLogger(0).WriteDebug(ex.Message);
            }

            finally
            {
                if (_fileChanging.CurrentCount == 0)
                {
                    _fileChanging.Release(1);
                }
            }
        }

        public async Task Initialize(IManager mgr)
        {
            // for some reason we get an event trigger when the file is not finished being modified.
            // this must have been a change in .NET CORE 3.x
            // so if the new file is empty we can't process it yet
            if (new FileInfo(FileName).Length == 0L)
            {
                return;
            }

            bool firstRun = ScriptEngine == null;

            // it's been loaded before so we need to call the unload event
            if (!firstRun)
            {
                await OnUnloadAsync();
            }

            successfullyLoaded = false;
            Manager = mgr;
            string script;

            using (var stream = new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var reader = new StreamReader(stream, Encoding.Default))
                {
                    script = await reader.ReadToEndAsync();
                }
            }

            ScriptEngine = new Engine(cfg =>
                cfg.AllowClr(new[]
                {
                    typeof(System.Net.Http.HttpClient).Assembly,
                    typeof(EFClient).Assembly,
                    typeof(Utilities).Assembly,
                    typeof(Encoding).Assembly
                })
                .CatchClrExceptions());

            ScriptEngine.Execute(script);
            ScriptEngine.SetValue("_localization", Utilities.CurrentLocalization);
            dynamic pluginObject = ScriptEngine.GetValue("plugin").ToObject();

            Author = pluginObject.author;
            Name = pluginObject.name;
            Version = (float)pluginObject.version;

            try
            {
                if(pluginObject.isParser)
                {
                    await OnLoadAsync(mgr);
                    IEventParser eventParser = (IEventParser)ScriptEngine.GetValue("eventParser").ToObject();
                    IRConParser rconParser = (IRConParser)ScriptEngine.GetValue("rconParser").ToObject();
                    Manager.AdditionalEventParsers.Add(eventParser);
                    Manager.AdditionalRConParsers.Add(rconParser);
                }
            }
            catch { }


            if (!firstRun)
            {
                await OnLoadAsync(mgr);
            }

            successfullyLoaded = true;
        }

        public async Task OnEventAsync(GameEvent E, Server S)
        {
            if (successfullyLoaded)
            {
                try
                {
                    await _fileChanging.WaitAsync();
                    ScriptEngine.SetValue("_gameEvent", E);
                    ScriptEngine.SetValue("_server", S);
                    ScriptEngine.SetValue("_IW4MAdminClient", Utilities.IW4MAdminClient(S));
                    ScriptEngine.Execute("plugin.onEventAsync(_gameEvent, _server)").GetCompletionValue();
                }

                catch { }

                finally
                {
                    if (_fileChanging.CurrentCount == 0)
                    {
                        _fileChanging.Release(1);
                    }
                }
            }
        }

        public Task OnLoadAsync(IManager manager)
        {
            Manager.GetLogger(0).WriteDebug($"OnLoad executing for {Name}");
            ScriptEngine.SetValue("_manager", manager);
            return Task.FromResult(ScriptEngine.Execute("plugin.onLoadAsync(_manager)").GetCompletionValue());
        }

        public Task OnTickAsync(Server S)
        {
            ScriptEngine.SetValue("_server", S);
            return Task.FromResult(ScriptEngine.Execute("plugin.onTickAsync(_server)").GetCompletionValue());
        }

        public async Task OnUnloadAsync()
        {
            if (successfullyLoaded)
            {
                Manager.GetLogger(0).WriteDebug($"OnUnLoad executing for {Name}");
                await Task.FromResult(ScriptEngine.Execute("plugin.onUnloadAsync()").GetCompletionValue());
            }
        }
    }
}
