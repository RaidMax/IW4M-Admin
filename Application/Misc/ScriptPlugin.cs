using Jint;
using Microsoft.CSharp.RuntimeBinder;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IW4MAdmin.Application.Misc
{
    /// <summary>
    /// implementation of IPlugin
    /// used to proxy script plugin requests
    /// </summary>
    public class ScriptPlugin : IPlugin
    {
        public string Name { get; set; }

        public float Version { get; set; }

        public string Author { get; set; }

        /// <summary>
        /// indicates if the plugin is a parser
        /// </summary>
        public bool IsParser { get; private set; }

        public FileSystemWatcher Watcher { get; private set; }

        private Engine _scriptEngine;
        private readonly string _fileName;
        private readonly SemaphoreSlim _onProcessing;
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
            _onProcessing = new SemaphoreSlim(1, 1);
        }

        ~ScriptPlugin()
        {
            Watcher.Dispose();
            _onProcessing.Dispose();
        }

        public async Task Initialize(IManager manager)
        {
            await _onProcessing.WaitAsync();

            try
            {
                // for some reason we get an event trigger when the file is not finished being modified.
                // this must have been a change in .NET CORE 3.x
                // so if the new file is empty we can't process it yet
                if (new FileInfo(_fileName).Length == 0L)
                {
                    return;
                }

                bool firstRun = _scriptEngine == null;

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

                _scriptEngine = new Engine(cfg =>
                    cfg.AllowClr(new[]
                    {
                    typeof(System.Net.Http.HttpClient).Assembly,
                    typeof(EFClient).Assembly,
                    typeof(Utilities).Assembly,
                    typeof(Encoding).Assembly
                    })
                    .CatchClrExceptions());

                _scriptEngine.Execute(script);
                _scriptEngine.SetValue("_localization", Utilities.CurrentLocalization);
                dynamic pluginObject = _scriptEngine.GetValue("plugin").ToObject();

                Author = pluginObject.author;
                Name = pluginObject.name;
                Version = (float)pluginObject.version;

                await OnLoadAsync(manager);

                try
                {
                    if (pluginObject.isParser)
                    {
                        IsParser = true;
                        IEventParser eventParser = (IEventParser)_scriptEngine.GetValue("eventParser").ToObject();
                        IRConParser rconParser = (IRConParser)_scriptEngine.GetValue("rconParser").ToObject();
                        manager.AdditionalEventParsers.Add(eventParser);
                        manager.AdditionalRConParsers.Add(rconParser);
                    }
                }

                catch (RuntimeBinderException) { }

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
                if (_onProcessing.CurrentCount == 0)
                {
                    _onProcessing.Release(1);
                }
            }
        }

        public async Task OnEventAsync(GameEvent E, Server S)
        {
            if (successfullyLoaded)
            {
                await _onProcessing.WaitAsync();

                try
                {
                    _scriptEngine.SetValue("_gameEvent", E);
                    _scriptEngine.SetValue("_server", S);
                    _scriptEngine.SetValue("_IW4MAdminClient", Utilities.IW4MAdminClient(S));
                    _scriptEngine.Execute("plugin.onEventAsync(_gameEvent, _server)").GetCompletionValue();
                }

                catch
                {
                    throw;
                }

                finally
                {
                    if (_onProcessing.CurrentCount == 0)
                    {
                        _onProcessing.Release(1);
                    }
                }
            }
        }

        public Task OnLoadAsync(IManager manager)
        {
            manager.GetLogger(0).WriteDebug($"OnLoad executing for {Name}");
            _scriptEngine.SetValue("_manager", manager);
            return Task.FromResult(_scriptEngine.Execute("plugin.onLoadAsync(_manager)").GetCompletionValue());
        }

        public Task OnTickAsync(Server S)
        {
            _scriptEngine.SetValue("_server", S);
            return Task.FromResult(_scriptEngine.Execute("plugin.onTickAsync(_server)").GetCompletionValue());
        }

        public async Task OnUnloadAsync()
        {
            if (successfullyLoaded)
            {
                await Task.FromResult(_scriptEngine.Execute("plugin.onUnloadAsync()").GetCompletionValue());
            }
        }
    }
}
