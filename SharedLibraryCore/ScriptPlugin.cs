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

        public FileSystemWatcher Watcher { get; private set; }

        private Engine ScriptEngine;
        private readonly string _fileName;
        private readonly SemaphoreSlim _fileChanging;
        private bool successfullyLoaded;

        public ScriptPlugin(string filename)
        {
            _fileName = filename;
            Watcher = new FileSystemWatcher()
            {
                Path = $"{Utilities.OperatingDirectory}Plugins{Path.DirectorySeparatorChar}",
                NotifyFilter = NotifyFilters.Size,
                Filter = _fileName.Split(Path.DirectorySeparatorChar).Last()
            };

            Watcher.EnableRaisingEvents = true;
            _fileChanging = new SemaphoreSlim(1, 1);
        }

        ~ScriptPlugin()
        {
            Watcher.Dispose();
            _fileChanging.Dispose();
        }


        public async Task Initialize(IManager manager)
        {
            await _fileChanging.WaitAsync();

            try
            {
                // for some reason we get an event trigger when the file is not finished being modified.
                // this must have been a change in .NET CORE 3.x
                // so if the new file is empty we can't process it yet
                if (new FileInfo(_fileName).Length == 0L)
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
                string script;

                using (var stream = new FileStream(_fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
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
                    if (pluginObject.isParser)
                    {
                        await OnLoadAsync(manager);
                        IEventParser eventParser = (IEventParser)ScriptEngine.GetValue("eventParser").ToObject();
                        IRConParser rconParser = (IRConParser)ScriptEngine.GetValue("rconParser").ToObject();
                        manager.AdditionalEventParsers.Add(eventParser);
                        manager.AdditionalRConParsers.Add(rconParser);
                    }
                }
                catch { }


                if (!firstRun)
                {
                    await OnLoadAsync(manager);
                }

                successfullyLoaded = true;
            }

            catch
            {
                throw;
            }

            finally
            {
                if (_fileChanging.CurrentCount == 0)
                {
                    _fileChanging.Release(1);
                }
            }
        }

        public async Task OnEventAsync(GameEvent E, Server S)
        {
            if (successfullyLoaded)
            {
                ScriptEngine.SetValue("_gameEvent", E);
                ScriptEngine.SetValue("_server", S);
                ScriptEngine.SetValue("_IW4MAdminClient", Utilities.IW4MAdminClient(S));
                await Task.FromResult(ScriptEngine.Execute("plugin.onEventAsync(_gameEvent, _server)").GetCompletionValue());
            }
        }

        public Task OnLoadAsync(IManager manager)
        {
            manager.GetLogger(0).WriteDebug($"OnLoad executing for {Name}");
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
                await Task.FromResult(ScriptEngine.Execute("plugin.onUnloadAsync()").GetCompletionValue());
            }
        }
    }
}
