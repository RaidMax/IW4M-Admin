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
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IW4MAdmin.Application.Extensions;
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
                NotifyFilter = NotifyFilters.LastWrite,
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
                    cfg.AddExtensionMethods(typeof(Utilities), typeof(Enumerable), typeof(Queryable),
                            typeof(ScriptPluginExtensions))
                        .AllowClr(new[]
                        {
                            typeof(System.Net.Http.HttpClient).Assembly,
                            typeof(EFClient).Assembly,
                            typeof(Utilities).Assembly,
                            typeof(Encoding).Assembly,
                            typeof(CancellationTokenSource).Assembly,
                            typeof(Data.Models.Client.EFClient).Assembly,
                            typeof(IW4MAdmin.Plugins.Stats.Plugin).Assembly
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

                async Task<bool> OnLoadTask()
                {
                    await OnLoadAsync(manager);
                    return true;
                }

                var loadComplete = false;
                
                try
                {
                    if (pluginObject.isParser)
                    {
                        loadComplete = await OnLoadTask();
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

                    if (!loadComplete)
                    {
                        _scriptEngine.SetValue("_configHandler", configWrapper);
                        loadComplete = await OnLoadTask();
                    }
                }

                if (!firstRun && !loadComplete)
                {
                    loadComplete = await OnLoadTask();
                }

                _successfullyLoaded = loadComplete;
            }
            catch (JavaScriptException ex)
            {
                _logger.LogError(ex,
                    "Encountered JavaScript runtime error while executing {MethodName} for script plugin {Plugin} at {@LocationInfo} StackTrace={StackTrace}",
                    nameof(Initialize), Path.GetFileName(_fileName), ex.Location, ex.JavaScriptStackTrace);

                throw new PluginException("An error occured while initializing script plugin");
            }
            catch (Exception ex) when (ex.InnerException is JavaScriptException jsEx)
            {
                _logger.LogError(ex,
                    "Encountered JavaScript runtime error while executing {MethodName} for script plugin {Plugin} initialization {@LocationInfo} StackTrace={StackTrace}",
                    nameof(Initialize), _fileName, jsEx.Location, jsEx.JavaScriptStackTrace);

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
                WrapJavaScriptErrorHandling(() =>
                {
                    _scriptEngine.SetValue("_gameEvent", gameEvent);
                    _scriptEngine.SetValue("_server", server);
                    _scriptEngine.SetValue("_IW4MAdminClient", Utilities.IW4MAdminClient(server));
                    return _scriptEngine.Evaluate("plugin.onEventAsync(_gameEvent, _server)");
                }, new { EventType = gameEvent.Type }, server);
            }
            finally
            {
                if (_onProcessing.CurrentCount == 0)
                {
                    _onProcessing.Release(1);
                }
            }
            
        }

        public  Task OnLoadAsync(IManager manager)
        {
            _logger.LogDebug("OnLoad executing for {Name}", Name);

            WrapJavaScriptErrorHandling(() =>
            {
                _scriptEngine.SetValue("_manager", manager);
                _scriptEngine.SetValue("getDvar", BeginGetDvar);
                _scriptEngine.SetValue("setDvar", BeginSetDvar);
                return _scriptEngine.Evaluate("plugin.onLoadAsync(_manager)");
            });

            return Task.CompletedTask;
        }

        public Task OnTickAsync(Server server)
        {
            WrapJavaScriptErrorHandling(() =>
            {
                _scriptEngine.SetValue("_server", server);
                return _scriptEngine.Evaluate("plugin.onTickAsync(_server)");
            });

            return Task.CompletedTask;
        }

        public async Task OnUnloadAsync()
        {
            if (!_successfullyLoaded)
            {
                return;
            }

            try
            {
                await _onProcessing.WaitAsync();

                _logger.LogDebug("OnUnload executing for {Name}", Name);

                WrapJavaScriptErrorHandling(() => _scriptEngine.Evaluate("plugin.onUnloadAsync()"));
            }
            finally
            {
                if (_onProcessing.CurrentCount == 0)
                {
                    _onProcessing.Release(1);
                }
            }
        }

        public T ExecuteAction<T>(Delegate action, CancellationToken token, params object[] param)
        {
            try
            {
                using var forceTimeout = new CancellationTokenSource(5000);
                using var combined = CancellationTokenSource.CreateLinkedTokenSource(forceTimeout.Token, token);
                _onProcessing.Wait(combined.Token);
                
                _logger.LogDebug("Executing action for {Name}", Name);
                
                return WrapJavaScriptErrorHandling(T() =>
                    {
                        var args = param.Select(p => JsValue.FromObject(_scriptEngine, p)).ToArray();
                        var result = action.DynamicInvoke(JsValue.Undefined, args);
                        return (T)(result as JsValue)?.ToObject();
                    },
         new
                    {
                        Params = string.Join(", ",
                            param?.Select(eachParam => $"Type={eachParam?.GetType().Name} Value={eachParam}") ??
                            Enumerable.Empty<string>())
                    });
            }
            finally
            {
                if (_onProcessing.CurrentCount == 0)
                {
                    _onProcessing.Release(1);
                }
            }
        }

        public T WrapDelegate<T>(Delegate act, CancellationToken token, params object[] args)
        {
            try
            {
                using var forceTimeout = new CancellationTokenSource(5000);
                using var combined = CancellationTokenSource.CreateLinkedTokenSource(forceTimeout.Token, token);
                _onProcessing.Wait(combined.Token);

                _logger.LogDebug("Wrapping delegate action for {Name}", Name);

                return WrapJavaScriptErrorHandling(
                    T() => (T)(act.DynamicInvoke(JsValue.Null,
                            args.Select(arg => JsValue.FromObject(_scriptEngine, arg)).ToArray()) as ObjectWrapper)
                        ?.ToObject(),
                    new
                    {
                        Params = string.Join(", ",
                            args?.Select(eachParam => $"Type={eachParam?.GetType().Name} Value={eachParam}") ??
                            Enumerable.Empty<string>())
                    });
            }
            finally
            {
                if (_onProcessing.CurrentCount == 0)
                {
                    _onProcessing.Release(1);
                }
            }
        }

        /// <summary>
        /// finds declared script commands in the script plugin
        /// </summary>
        /// <param name="commands">commands value from jint parser</param>
        /// <param name="scriptCommandFactory">factory to create the command from</param>
        /// <returns></returns>
        private IEnumerable<IManagerCommand> GenerateScriptCommands(JsValue commands,
            IScriptCommandFactory scriptCommandFactory)
        {
            var commandList = new List<IManagerCommand>();

            // go through each defined command
            foreach (var command in commands.AsArray())
            {
                dynamic dynamicCommand = command.ToObject();
                string name = dynamicCommand.name;
                string alias = dynamicCommand.alias;
                string description = dynamicCommand.description;

                if (dynamicCommand.permission is Data.Models.Client.EFClient.Permission perm)
                {
                    dynamicCommand.permission = perm.ToString();
                }

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

        private void BeginGetDvar(Server server, string dvarName, Delegate onCompleted)
        {
            var operationTimeout = TimeSpan.FromSeconds(5);

            void OnComplete(IAsyncResult result)
            {
                try
                {
                    _onProcessing.Wait();

                    var (success, value) = (ValueTuple<bool, string>)result.AsyncState;
                    onCompleted.DynamicInvoke(JsValue.Undefined,
                        new[]
                        {
                            JsValue.FromObject(_scriptEngine, server),
                            JsValue.FromObject(_scriptEngine, dvarName),
                            JsValue.FromObject(_scriptEngine, value),
                            JsValue.FromObject(_scriptEngine, success)
                        });
                }
                catch (JavaScriptException ex)
                {
                    using (LogContext.PushProperty("Server", server.ToString()))
                    {
                        _logger.LogError(ex, "Could not invoke BeginGetDvar callback for {Filename} {@Location}",
                            Path.GetFileName(_fileName), ex.Location);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not complete {BeginGetDvar} for {Class}", nameof(BeginGetDvar), Name);
                }
                finally
                {
                    if (_onProcessing.CurrentCount == 0)
                    {
                        _onProcessing.Release(1);
                    }
                }
            }

            new Thread(() =>
            {
                if (DateTime.Now - (server.MatchEndTime ?? server.MatchStartTime) < TimeSpan.FromSeconds(15))
                {
                    using (LogContext.PushProperty("Server", server.ToString()))
                    {
                        _logger.LogDebug("Not getting DVar because match recently ended");
                    }

                    OnComplete(new AsyncResult
                    {
                        IsCompleted = false,
                        AsyncState = (false, (string)null)
                    });
                }

                using var tokenSource = new CancellationTokenSource();
                tokenSource.CancelAfter(operationTimeout);

                server.GetDvarAsync<string>(dvarName, token: tokenSource.Token).ContinueWith(action =>
                {
                    if (action.IsCompletedSuccessfully)
                    {
                        OnComplete(new AsyncResult
                        {
                            IsCompleted = true,
                            AsyncState = (true, action.Result.Value)
                        });
                    }
                    else
                    {
                        OnComplete(new AsyncResult
                        {
                            IsCompleted = false,
                            AsyncState = (false, (string)null)
                        });
                    }
                });
            }).Start();
        }

        private void BeginSetDvar(Server server, string dvarName, string dvarValue, Delegate onCompleted)
        {
            var operationTimeout = TimeSpan.FromSeconds(5);

            void OnComplete(IAsyncResult result)
            {
                try
                {
                    _onProcessing.Wait();
                    var success = (bool)result.AsyncState;
                    onCompleted.DynamicInvoke(JsValue.Undefined,
                        new[]
                        {
                            JsValue.FromObject(_scriptEngine, server),
                            JsValue.FromObject(_scriptEngine, dvarName),
                            JsValue.FromObject(_scriptEngine, dvarValue),
                            JsValue.FromObject(_scriptEngine, success)
                        });
                }
                catch (JavaScriptException ex)
                {
                    using (LogContext.PushProperty("Server", server.ToString()))
                    {
                        _logger.LogError(ex, "Could complete BeginSetDvar for {Filename} {@Location}",
                            Path.GetFileName(_fileName), ex.Location);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not complete {BeginSetDvar} for {Class}", nameof(BeginSetDvar), Name);
                }
                finally
                {
                    if (_onProcessing.CurrentCount == 0)
                    {
                        _onProcessing.Release(1);
                    }
                }
            }

            new Thread(() =>
            {
                if (DateTime.Now - (server.MatchEndTime ?? server.MatchStartTime) < TimeSpan.FromSeconds(15))
                {
                    using (LogContext.PushProperty("Server", server.ToString()))
                    {
                        _logger.LogDebug("Not setting DVar because match recently ended");
                    }

                    OnComplete(new AsyncResult
                    {
                        IsCompleted = false,
                        AsyncState = false
                    });
                }

                using var tokenSource = new CancellationTokenSource();
                tokenSource.CancelAfter(operationTimeout);

                server.SetDvarAsync(dvarName, dvarValue, token: tokenSource.Token).ContinueWith(action =>
                {
                    if (action.IsCompletedSuccessfully)
                    {
                        OnComplete(new AsyncResult
                        {
                            IsCompleted = true,
                            AsyncState = true
                        });
                    }
                    else
                    {
                        OnComplete(new AsyncResult
                        {
                            IsCompleted = false,
                            AsyncState = false
                        });
                    }
                });
            }).Start();
        }

        private T WrapJavaScriptErrorHandling<T>(Func<T> work, object additionalData = null, Server server = null,
            [CallerMemberName] string methodName = "")
        {
            using (LogContext.PushProperty("Server", server?.ToString()))
            {
                try
                {
                    return work();
                }
                catch (JavaScriptException ex)
                {
                    _logger.LogError(ex,
                        "Encountered JavaScript runtime error while executing {MethodName} for script plugin {Plugin} at {@LocationInfo} StackTrace={StackTrace} {@AdditionalData}",
                        methodName, Path.GetFileName(_fileName), ex.Location, ex.StackTrace, additionalData);

                    throw new PluginException("A runtime error occured while executing action for script plugin");
                }
                catch (Exception ex) when (ex.InnerException is JavaScriptException jsEx)
                {
                    _logger.LogError(ex,
                        "Encountered JavaScript runtime error while executing {MethodName} for script plugin {Plugin} initialization {@LocationInfo} StackTrace={StackTrace} {@AdditionalData}",
                        methodName, _fileName, jsEx.Location, jsEx.JavaScriptStackTrace, additionalData);

                    throw new PluginException("A runtime error occured while executing action for script plugin");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Encountered JavaScript runtime error while executing {MethodName} for script plugin {Plugin}",
                        methodName, Path.GetFileName(_fileName));

                    throw new PluginException("An error occured while executing action for script plugin");
                }
            }
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
