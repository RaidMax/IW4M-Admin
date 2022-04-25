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
using Jint.Runtime.Interop;
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

        public FileSystemWatcher Watcher { get; }

        private Engine _scriptEngine;
        private readonly string _fileName;
        private readonly SemaphoreSlim _onProcessing = new(1, 1);
        private bool _successfullyLoaded;
        private readonly List<string> _registeredCommandNames;
        private readonly ILogger _logger;

        public ScriptPlugin(ILogger logger, string filename, string workingDirectory = null)
        {
            _logger = logger;
            _fileName = filename;
            Watcher = new FileSystemWatcher
            {
                Path = workingDirectory ?? $"{Utilities.OperatingDirectory}Plugins{Path.DirectorySeparatorChar}",
                NotifyFilter = NotifyFilters.Size,
                Filter = _fileName.Split(Path.DirectorySeparatorChar).Last()
            };

            Watcher.EnableRaisingEvents = true;
            _registeredCommandNames = new List<string>();
        }

        ~ScriptPlugin()
        {
            Watcher.Dispose();
            _onProcessing.Dispose();
        }

        public async Task Initialize(IManager manager, IScriptCommandFactory scriptCommandFactory,
            IScriptPluginServiceResolver serviceResolver)
        {
            try
            {
                await _onProcessing.WaitAsync();

                // for some reason we get an event trigger when the file is not finished being modified.
                // this must have been a change in .NET CORE 3.x
                // so if the new file is empty we can't process it yet
                if (new FileInfo(_fileName).Length == 0L)
                {
                    return;
                }

                var firstRun = _scriptEngine == null;

                // it's been loaded before so we need to call the unload event
                if (!firstRun)
                {
                    await OnUnloadAsync();

                    foreach (var commandName in _registeredCommandNames)
                    {
                        _logger.LogDebug("Removing plugin registered command {Command}", commandName);
                        manager.RemoveCommandByName(commandName);
                    }

                    _registeredCommandNames.Clear();
                }

                _successfullyLoaded = false;
                string script;

                await using (var stream =
                             new FileStream(_fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
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
                        .CatchClrExceptions()
                        .AddObjectConverter(new PermissionLevelToStringConverter()));

                _scriptEngine.Execute(script);
                _scriptEngine.SetValue("_localization", Utilities.CurrentLocalization);
                _scriptEngine.SetValue("_serviceResolver", serviceResolver);
                _scriptEngine.SetValue("_lock", _onProcessing);
                dynamic pluginObject = _scriptEngine.Evaluate("plugin").ToObject();

                Author = pluginObject.author;
                Name = pluginObject.name;
                Version = (float)pluginObject.version;

                var commands = JsValue.Undefined;
                try
                {
                    commands = _scriptEngine.Evaluate("commands");
                }
                catch (JavaScriptException)
                {
                    // ignore because commands aren't defined;
                }

                if (commands != JsValue.Undefined)
                {
                    try
                    {
                        foreach (var command in GenerateScriptCommands(commands, scriptCommandFactory))
                        {
                            _logger.LogDebug("Adding plugin registered command {CommandName}", command.Name);
                            manager.AddAdditionalCommand(command);
                            _registeredCommandNames.Add(command.Name);
                        }
                    }

                    catch (RuntimeBinderException e)
                    {
                        throw new PluginException($"Not all required fields were found: {e.Message}")
                            { PluginFile = _fileName };
                    }
                }

                try
                {
                    if (pluginObject.isParser)
                    {
                        await OnLoadAsync(manager);
                        IsParser = true;
                        var eventParser = (IEventParser)_scriptEngine.Evaluate("eventParser").ToObject();
                        var rconParser = (IRConParser)_scriptEngine.Evaluate("rconParser").ToObject();
                        manager.AdditionalEventParsers.Add(eventParser);
                        manager.AdditionalRConParsers.Add(rconParser);
                    }
                }

                catch (RuntimeBinderException)
                {
                    var configWrapper = new ScriptPluginConfigurationWrapper(Name, _scriptEngine);
                    await configWrapper.InitializeAsync();
                    _scriptEngine.SetValue("_configHandler", configWrapper);
                    await OnLoadAsync(manager);
                }

                if (!firstRun)
                {
                    await OnLoadAsync(manager);
                }

                _successfullyLoaded = true;
            }
            catch (JavaScriptException ex)
            {
                _logger.LogError(ex,
                    "Encountered JavaScript runtime error while executing {MethodName} for script plugin {Plugin} at {@LocationInfo}",
                    nameof(Initialize), Path.GetFileName(_fileName), ex.Location);

                throw new PluginException("An error occured while initializing script plugin");
            }
            catch (Exception ex) when (ex.InnerException is JavaScriptException jsEx)
            {
                _logger.LogError(ex,
                    "Encountered JavaScript runtime error while executing {MethodName} for script plugin {Plugin} initialization {@LocationInfo}",
                    nameof(Initialize), _fileName, jsEx.Location);

                throw new PluginException("An error occured while initializing script plugin");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Encountered JavaScript runtime error while executing {MethodName} for script plugin {Plugin}",
                    nameof(OnLoadAsync), Path.GetFileName(_fileName));

                throw new PluginException("An error occured while executing action for script plugin");
            }
            finally
            {
                if (_onProcessing.CurrentCount == 0)
                {
                    _onProcessing.Release(1);
                }
            }
        }

        public async Task OnEventAsync(GameEvent gameEvent, Server server)
        {
            if (!_successfullyLoaded)
            {
                return;
            }

            try
            {
                await _onProcessing.WaitAsync();
                _scriptEngine.SetValue("_gameEvent", gameEvent);
                _scriptEngine.SetValue("_server", server);
                _scriptEngine.SetValue("_IW4MAdminClient", Utilities.IW4MAdminClient(server));
                _scriptEngine.Evaluate("plugin.onEventAsync(_gameEvent, _server)");
            }

            catch (JavaScriptException ex)
            {
                using (LogContext.PushProperty("Server", server.ToString()))
                {
                    _logger.LogError(ex,
                        "Encountered JavaScript runtime error while executing {MethodName} for script plugin {Plugin} with event type {EventType} {@LocationInfo}",
                        nameof(OnEventAsync), Path.GetFileName(_fileName), gameEvent.Type, ex.Location);
                }

                throw new PluginException("An error occured while executing action for script plugin");
            }

            catch (Exception ex)
            {
                using (LogContext.PushProperty("Server", server.ToString()))
                {
                    _logger.LogError(ex,
                        "Encountered error while running {MethodName} for script plugin {Plugin} with event type {EventType}",
                        nameof(OnEventAsync), _fileName, gameEvent.Type);
                }

                throw new PluginException("An error occured while executing action for script plugin");
            }

            finally
            {
                if (_onProcessing.CurrentCount == 0)
                {
                    _onProcessing.Release(1);
                }
            }
        }

        public Task OnLoadAsync(IManager manager)
        {
            try
            {
                _logger.LogDebug("OnLoad executing for {Name}", Name);
                _scriptEngine.SetValue("_manager", manager);
                _scriptEngine.SetValue("getDvar", GetDvarAsync);
                _scriptEngine.SetValue("setDvar", SetDvarAsync);
                _scriptEngine.Evaluate("plugin.onLoadAsync(_manager)");

                return Task.CompletedTask;
            }
            catch (JavaScriptException ex)
            {
                _logger.LogError(ex,
                    "Encountered JavaScript runtime error while executing {MethodName} for script plugin {Plugin} at {@LocationInfo}",
                    nameof(OnLoadAsync), Path.GetFileName(_fileName), ex.Location);

                throw new PluginException("A runtime error occured while executing action for script plugin");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Encountered JavaScript runtime error while executing {MethodName} for script plugin {Plugin}",
                    nameof(OnLoadAsync), Path.GetFileName(_fileName));

                throw new PluginException("An error occured while executing action for script plugin");
            }
        }

        public async Task OnTickAsync(Server server)
        {
            _scriptEngine.SetValue("_server", server);
            await Task.FromResult(_scriptEngine.Evaluate("plugin.onTickAsync(_server)"));
        }

        public Task OnUnloadAsync()
        {
            if (!_successfullyLoaded)
            {
                return Task.CompletedTask;
            }

            try
            {
                _scriptEngine.Evaluate("plugin.onUnloadAsync()");
            }
            catch (JavaScriptException ex)
            {
                _logger.LogError(ex,
                    "Encountered JavaScript runtime error while executing {MethodName} for script plugin {Plugin} at {@LocationInfo}",
                    nameof(OnUnloadAsync), Path.GetFileName(_fileName), ex.Location);

                throw new PluginException("A runtime error occured while executing action for script plugin");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Encountered JavaScript runtime error while executing {MethodName} for script plugin {Plugin}",
                    nameof(OnUnloadAsync), Path.GetFileName(_fileName));

                throw new PluginException("An error occured while executing action for script plugin");
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// finds declared script commands in the script plugin
        /// </summary>
        /// <param name="commands">commands value from jint parser</param>
        /// <param name="scriptCommandFactory">factory to create the command from</param>
        /// <returns></returns>
        private IEnumerable<IManagerCommand> GenerateScriptCommands(JsValue commands, IScriptCommandFactory scriptCommandFactory)
        {
            var commandList = new List<IManagerCommand>();

            // go through each defined command
            foreach (var command in commands.AsArray())
            {
                dynamic dynamicCommand = command.ToObject();
                string name = dynamicCommand.name;
                string alias = dynamicCommand.alias;
                string description = dynamicCommand.description;
                string permission = dynamicCommand.permission;
                List<Server.Game> supportedGames = null;
                var targetRequired = false;

                var args = new List<(string, bool)>();
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

                try
                {
                    foreach (var game in dynamicCommand.supportedGames)
                    {
                        supportedGames ??= new List<Server.Game>();
                        supportedGames.Add(Enum.Parse(typeof(Server.Game), game.ToString()));
                    }
                }
                catch (RuntimeBinderException)
                {
                    // supported games is optional
                }

                async Task Execute(GameEvent gameEvent)
                {
                    try
                    {
                        await _onProcessing.WaitAsync();

                        _scriptEngine.SetValue("_event", gameEvent);
                        var jsEventObject = _scriptEngine.Evaluate("_event");
                        
                        dynamicCommand.execute.Target.Invoke(_scriptEngine, jsEventObject);
                    }

                    catch (JavaScriptException ex)
                    {
                        using (LogContext.PushProperty("Server", gameEvent.Owner?.ToString()))
                        {
                            _logger.LogError(ex, "Could not execute command action for {Filename} {@Location}",
                                Path.GetFileName(_fileName), ex.Location);
                        }

                        throw new PluginException("A runtime error occured while executing action for script plugin");
                    }
                    
                    catch (Exception ex)
                    {
                        using (LogContext.PushProperty("Server", gameEvent.Owner?.ToString()))
                        {
                            _logger.LogError(ex,
                                "Could not execute command action for script plugin {FileName}",
                                Path.GetFileName(_fileName));
                        }

                        throw new PluginException("An error occured while executing action for script plugin");
                    }


                    finally
                    {
                        if (_onProcessing.CurrentCount == 0)
                        {
                            _onProcessing.Release(1);
                        }
                    }
                }

                commandList.Add(scriptCommandFactory.CreateScriptCommand(name, alias, description, permission,
                    targetRequired, args, Execute, supportedGames?.ToArray()));
            }

            return commandList;
        }

        private void GetDvarAsync(Server server, string dvarName, Delegate onCompleted)
        {
            Task.Run(() =>
            {
                var tokenSource = new CancellationTokenSource();
                tokenSource.CancelAfter(TimeSpan.FromSeconds(5));
                string result = null;
                var success = true;
                try
                {
                    result = server.GetDvarAsync<string>(dvarName, token: tokenSource.Token).GetAwaiter().GetResult().Value;
                }
                catch
                {
                    success = false;
                }

                _onProcessing.Wait();
                try
                {
                    onCompleted.DynamicInvoke(JsValue.Undefined,
                        new[]
                        {
                            JsValue.FromObject(_scriptEngine, server), 
                            JsValue.FromObject(_scriptEngine, dvarName),
                            JsValue.FromObject(_scriptEngine, result),
                            JsValue.FromObject(_scriptEngine, success), 
                        });
                }

                finally
                {
                    if (_onProcessing.CurrentCount == 0)
                    {
                        _onProcessing.Release();
                    }
                }
            });
        }
        private void SetDvarAsync(Server server, string dvarName, string dvarValue, Delegate onCompleted)
        {
            Task.Run(() =>
            {
                var tokenSource = new CancellationTokenSource();
                tokenSource.CancelAfter(TimeSpan.FromSeconds(5));
                var success = true;

                try
                {
                    server.SetDvarAsync(dvarName, dvarValue, tokenSource.Token).GetAwaiter().GetResult();
                }
                catch
                {
                    success = false;
                }

                _onProcessing.Wait();
                try
                {
                    onCompleted.DynamicInvoke(JsValue.Undefined,
                        new[]
                        {
                            JsValue.FromObject(_scriptEngine, server),
                            JsValue.FromObject(_scriptEngine, dvarName), 
                            JsValue.FromObject(_scriptEngine, dvarValue),
                            JsValue.FromObject(_scriptEngine, success)
                        });
                }

                finally
                {
                    if (_onProcessing.CurrentCount == 0)
                    {
                        _onProcessing.Release();
                    }
                }
            });
        }
    }

    public class PermissionLevelToStringConverter : IObjectConverter
    {
        public bool TryConvert(Engine engine, object value, out JsValue result)
        {
            if (value is Data.Models.Client.EFClient.Permission)
            {
                result = value.ToString();
                return true;
            }


            result = JsValue.Null;
            return false;
        }
    }
}
