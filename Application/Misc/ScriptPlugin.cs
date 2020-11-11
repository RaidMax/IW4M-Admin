using System;
using Jint;
using Jint.Native;
using Jint.Runtime;
using Microsoft.CSharp.RuntimeBinder;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Exceptions;
using SharedLibraryCore.Interfaces;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using ILogger = Microsoft.Extensions.Logging.ILogger;

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
        private readonly List<string> _registeredCommandNames;
        private readonly ILogger _logger;

        public ScriptPlugin(ILogger logger, string filename, string workingDirectory = null)
        {
            _logger = logger;
            _fileName = filename;
            Watcher = new FileSystemWatcher()
            {
                Path = workingDirectory == null ? $"{Utilities.OperatingDirectory}Plugins{Path.DirectorySeparatorChar}" : workingDirectory,
                NotifyFilter = NotifyFilters.Size,
                Filter = _fileName.Split(Path.DirectorySeparatorChar).Last()
            };

            Watcher.EnableRaisingEvents = true;
            _onProcessing = new SemaphoreSlim(1, 1);
            _registeredCommandNames = new List<string>();
        }

        ~ScriptPlugin()
        {
            Watcher.Dispose();
            _onProcessing.Dispose();
        }

        public async Task Initialize(IManager manager, IScriptCommandFactory scriptCommandFactory, IScriptPluginServiceResolver serviceResolver)
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

                    foreach (string commandName in _registeredCommandNames)
                    {
                        _logger.LogDebug("Removing plugin registered command {command}", commandName);
                        manager.RemoveCommandByName(commandName);
                    }

                    _registeredCommandNames.Clear();
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
                _scriptEngine.SetValue("_serviceResolver", serviceResolver);
                dynamic pluginObject = _scriptEngine.GetValue("plugin").ToObject();

                Author = pluginObject.author;
                Name = pluginObject.name;
                Version = (float)pluginObject.version;

                var commands = _scriptEngine.GetValue("commands");

                if (commands != JsValue.Undefined)
                {
                    try
                    {
                        foreach (var command in GenerateScriptCommands(commands, scriptCommandFactory))
                        {
                            _logger.LogDebug("Adding plugin registered command {commandName}", command.Name);
                            manager.AddAdditionalCommand(command);
                            _registeredCommandNames.Add(command.Name);
                        }
                    }

                    catch (RuntimeBinderException e)
                    {
                        throw new PluginException($"Not all required fields were found: {e.Message}") { PluginFile = _fileName };
                    }
                }

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

            catch (JavaScriptException ex)
            {
                _logger.LogError(ex,
                    "Encountered JavaScript runtime error while executing {methodName} for script plugin {plugin} initialization {@locationInfo}",
                    nameof(OnLoadAsync), _fileName, ex.Location);
                
                throw new PluginException("An error occured while initializing script plugin");
            }
            
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Encountered unexpected error while running {methodName} for script plugin {plugin} with event type {eventType}",
                    nameof(OnLoadAsync), _fileName);
                
                throw new PluginException("An unexpected error occured while initializing script plugin");
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
                
                catch (JavaScriptException ex)
                {
                    using (LogContext.PushProperty("Server", S.ToString()))
                    {
                        _logger.LogError(ex,
                            "Encountered JavaScript runtime error while executing {methodName} for script plugin {plugin} with event type {eventType} {@locationInfo}",
                            nameof(OnEventAsync), _fileName, E.Type, ex.Location);
                    }

                    throw new PluginException($"An error occured while executing action for script plugin");
                }

                catch (Exception e)
                {
                    using (LogContext.PushProperty("Server", S.ToString()))
                    {
                        _logger.LogError(e,
                            "Encountered unexpected error while running {methodName} for script plugin {plugin} with event type {eventType}",
                            nameof(OnEventAsync), _fileName, E.Type);
                    }

                    throw new PluginException($"An error occured while executing action for script plugin");
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
            _logger.LogDebug("OnLoad executing for {name}", Name);
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

        /// <summary>
        /// finds declared script commands in the script plugin
        /// </summary>
        /// <param name="commands">commands value from jint parser</param>
        /// <param name="scriptCommandFactory">factory to create the command from</param>
        /// <returns></returns>
        public IEnumerable<IManagerCommand> GenerateScriptCommands(JsValue commands, IScriptCommandFactory scriptCommandFactory)
        {
            List<IManagerCommand> commandList = new List<IManagerCommand>();

            // go through each defined command
            foreach (var command in commands.AsArray())
            {
                dynamic dynamicCommand = command.ToObject();
                string name = dynamicCommand.name;
                string alias = dynamicCommand.alias;
                string description = dynamicCommand.description;
                string permission = dynamicCommand.permission;
                bool targetRequired = false;

                List<(string, bool)> args = new List<(string, bool)>();
                dynamic arguments = null;

                try
                {
                    arguments = dynamicCommand.arguments;
                }

                catch (RuntimeBinderException)
                {
                    // arguments are optional
                }

                try
                {
                    targetRequired = dynamicCommand.targetRequired;
                }

                catch (RuntimeBinderException)
                {
                    // arguments are optional
                }

                if (arguments != null)
                {
                    foreach (var arg in dynamicCommand.arguments)
                    {
                        args.Add((arg.name, (bool)arg.required));
                    }
                }

                void execute(GameEvent e)
                {
                    _scriptEngine.SetValue("_event", e);
                    var jsEventObject = _scriptEngine.GetValue("_event");

                    try
                    {
                        dynamicCommand.execute.Target.Invoke(jsEventObject);
                    }

                    catch (JavaScriptException ex)
                    {
                        throw new PluginException($"An error occured while executing action for script plugin: {ex.Error} (Line: {ex.Location.Start.Line}, Character: {ex.Location.Start.Column})") { PluginFile = _fileName };
                    }
                }

                commandList.Add(scriptCommandFactory.CreateScriptCommand(name, alias, description, permission, targetRequired, args, execute));
            }

            return commandList;
        }
    }
}
