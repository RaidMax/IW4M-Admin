using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Data.Models;
using IW4MAdmin.Application.Configuration;
using IW4MAdmin.Application.Extensions;
using Jint;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Events.Server;
using SharedLibraryCore.Exceptions;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using JavascriptEngine = Jint.Engine;

namespace IW4MAdmin.Application.Plugin.Script;

public class ScriptPluginV2 : IPluginV2
{
    public string Name { get; private set; } = string.Empty;
    public string Author { get; private set; } = string.Empty;
    public string Version { get; private set; }

    private readonly string _fileName;
    private readonly ILogger<ScriptPluginV2> _logger;
    private readonly IScriptPluginServiceResolver _pluginServiceResolver;
    private readonly IScriptCommandFactory _scriptCommandFactory;
    private readonly IConfigurationHandlerV2<ScriptPluginConfiguration> _configHandler;
    private readonly IInteractionRegistration _interactionRegistration;
    private readonly SemaphoreSlim _onProcessingScript = new(1, 1);
    private readonly SemaphoreSlim _onLoadingFile = new(1, 1);
    private readonly FileSystemWatcher _scriptWatcher;
    private readonly List<string> _registeredCommandNames = new();
    private readonly List<string> _registeredInteractions = new();
    private readonly Dictionary<MethodInfo, List<object>> _registeredEvents = new();
    private IManager _manager;
    private bool _firstInitialization = true;

    private record ScriptPluginDetails(string Name, string Author, string Version,
        ScriptPluginCommandDetails[] Commands, ScriptPluginInteractionDetails[] Interactions);

    private record ScriptPluginCommandDetails(string Name, string Description, string Alias, string Permission,
        bool TargetRequired, CommandArgument[] Arguments, IEnumerable<Reference.Game> SupportedGames, Delegate Execute);

    private JavascriptEngine ScriptEngine
    {
        get
        {
            lock (ActiveEngines)
            {
                return ActiveEngines[$"{GetHashCode()}-{_nextEngineId}"];
            }
        }
    }

    private record ScriptPluginInteractionDetails(string Name, Delegate Action);

    private ScriptPluginConfigurationWrapper _scriptPluginConfigurationWrapper;
    private int _nextEngineId;
    private static readonly Dictionary<string, JavascriptEngine> ActiveEngines = new();

    public ScriptPluginV2(string fileName, ILogger<ScriptPluginV2> logger,
        IScriptPluginServiceResolver pluginServiceResolver, IScriptCommandFactory scriptCommandFactory,
        IConfigurationHandlerV2<ScriptPluginConfiguration> configHandler,
        IInteractionRegistration interactionRegistration)
    {
        _fileName = fileName;
        _logger = logger;
        _pluginServiceResolver = pluginServiceResolver;
        _scriptCommandFactory = scriptCommandFactory;
        _configHandler = configHandler;
        _interactionRegistration = interactionRegistration;
        _scriptWatcher = new FileSystemWatcher
        {
            Path = Path.Join(Utilities.OperatingDirectory, "Plugins"),
            NotifyFilter = NotifyFilters.LastWrite,
            Filter = _fileName.Split(Path.DirectorySeparatorChar).Last()
        };

        IManagementEventSubscriptions.Load += OnLoad;
    }

    public void ExecuteWithErrorHandling(Action<Engine> work)
    {
        WrapJavaScriptErrorHandling(() =>
        {
            work(ScriptEngine);
            return true;
        }, _logger, _fileName, _onProcessingScript);
    }

    public object QueryWithErrorHandling(Delegate action, params object[] args)
    {
        return WrapJavaScriptErrorHandling(() =>
        {
            var jsArgs = args?.Select(param => JsValue.FromObject(ScriptEngine, param)).ToArray();
            var result = action.DynamicInvoke(JsValue.Undefined, jsArgs);
            return result;
        }, _logger, _fileName, _onProcessingScript);
    }

    public void RegisterDynamicCommand(object command)
    {
        var parsedCommand = ParseScriptCommandDetails(command);
        RegisterCommand(_manager, parsedCommand.First());
    }

    private async Task OnLoad(IManager manager, CancellationToken token)
    {
        _manager = manager;
        var entered = false;
        try
        {
            await _onLoadingFile.WaitAsync(token);
            entered = true;

            _logger.LogDebug("{Method} executing for {Plugin}", nameof(OnLoad), _fileName);

            if (new FileInfo(_fileName).Length == 0L)
            {
                return;
            }

            _scriptWatcher.EnableRaisingEvents = false;

            UnregisterScriptEntities(manager);
            ResetEngineState();

            if (_firstInitialization)
            {
                _scriptWatcher.Changed += async (_, _) => await OnLoad(manager, token);
                _firstInitialization = false;
            }

            await using var stream =
                new FileStream(_fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.Default);
            var pluginScript = await reader.ReadToEndAsync();

            var pluginDetails = WrapJavaScriptErrorHandling(() =>
            {
                if (IsEngineDisposed(GetHashCode(), _nextEngineId))
                {
                    return null;
                }

                ScriptEngine.Execute(pluginScript);
#pragma warning disable CS8974
                var initResult = ScriptEngine.Call("init", JsValue.FromObject(ScriptEngine, EventCallbackWrapper),
                    JsValue.FromObject(ScriptEngine, _pluginServiceResolver),
                    JsValue.FromObject(ScriptEngine, _scriptPluginConfigurationWrapper),
                    JsValue.FromObject(ScriptEngine, new ScriptPluginHelper(manager, this)));
#pragma warning restore CS8974

                if (initResult.IsNull() || initResult.IsUndefined())
                {
                    return null;
                }

                return AsScriptPluginInstance(initResult.ToObject());
            }, _logger, _fileName, _onProcessingScript);

            if (pluginDetails is null)
            {
                _logger.LogInformation("No valid script plugin signature found for {FilePath}", _fileName);
                return;
            }

            foreach (var command in pluginDetails.Commands)
            {
                RegisterCommand(manager, command);

                _logger.LogDebug("Registered script plugin command {Command} for {Plugin}", command.Name,
                    pluginDetails.Name);
            }

            foreach (var interaction in pluginDetails.Interactions)
            {
                RegisterInteraction(interaction);

                _logger.LogDebug("Registered script plugin interaction {Interaction} for {Plugin}", interaction.Name,
                    pluginDetails.Name);
            }

            Name = pluginDetails.Name;
            Author = pluginDetails.Author;
            Version = pluginDetails.Version;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error encountered loading script plugin {Name}", _fileName);
        }
        finally
        {
            if (entered)
            {
                _onLoadingFile.Release(1);
                _scriptWatcher.EnableRaisingEvents = true;
            }

            _logger.LogDebug("{Method} completed for {Plugin}", nameof(OnLoad), _fileName);
        }
    }

    private void RegisterInteraction(ScriptPluginInteractionDetails interaction)
    {
        Task<IInteractionData> Action(int? targetId, Reference.Game? game, CancellationToken token) =>
            WrapJavaScriptErrorHandling(() =>
            {
                if (IsEngineDisposed(GetHashCode(), _nextEngineId))
                {
                    return null;
                }

                var args = new object[] { targetId, game, token }.Select(arg => JsValue.FromObject(ScriptEngine, arg))
                    .ToArray();

                if (interaction.Action.DynamicInvoke(JsValue.Undefined, args) is not ObjectWrapper result)
                {
                    throw new PluginException("Invalid interaction object returned");
                }

                return Task.FromResult((IInteractionData)result.ToObject());
            }, _logger, _fileName, _onProcessingScript);

        _interactionRegistration.RegisterInteraction(interaction.Name, Action);
        _registeredInteractions.Add(interaction.Name);
    }

    private void RegisterCommand(IManager manager, ScriptPluginCommandDetails command)
    {
        Task Execute(GameEvent gameEvent) =>
            WrapJavaScriptErrorHandling(() =>
            {
                if (IsEngineDisposed(GetHashCode(), _nextEngineId))
                {
                    return null;
                }

                command.Execute.DynamicInvoke(JsValue.Undefined,
                    new[] { JsValue.FromObject(ScriptEngine, gameEvent) });
                return Task.CompletedTask;
            }, _logger, _fileName, _onProcessingScript);

        var scriptCommand = _scriptCommandFactory.CreateScriptCommand(command.Name, command.Alias,
            command.Description,
            command.Permission, command.TargetRequired,
            command.Arguments, Execute, command.SupportedGames);

        manager.RemoveCommandByName(scriptCommand.Name);
        manager.AddAdditionalCommand(scriptCommand);
        if (!_registeredCommandNames.Contains(scriptCommand.Name))
        {
            _registeredCommandNames.Add(scriptCommand.Name);
        }
    }

    private void ResetEngineState()
    {
        JavascriptEngine oldEngine = null;

        lock (ActiveEngines)
        {
            if (ActiveEngines.ContainsKey($"{GetHashCode()}-{_nextEngineId}"))
            {
                oldEngine = ActiveEngines[$"{GetHashCode()}-{_nextEngineId}"];
                _logger.LogDebug("Removing script engine from active list {HashCode}", _nextEngineId);
                ActiveEngines.Remove($"{GetHashCode()}-{_nextEngineId}");
            }
        }

        Interlocked.Increment(ref _nextEngineId);
        oldEngine?.Dispose();
        var newEngine = new JavascriptEngine(cfg =>
            cfg.AddExtensionMethods(typeof(Utilities), typeof(Enumerable), typeof(Queryable),
                    typeof(ScriptPluginExtensions), typeof(LoggerExtensions))
                .AllowClr(typeof(System.Net.Http.HttpClient).Assembly, typeof(EFClient).Assembly,
                    typeof(Utilities).Assembly, typeof(Encoding).Assembly, typeof(CancellationTokenSource).Assembly,
                    typeof(Data.Models.Client.EFClient).Assembly, typeof(IW4MAdmin.Plugins.Stats.Plugin).Assembly, typeof(ScriptPluginWebRequest).Assembly)
                .CatchClrExceptions()
                .AddObjectConverter(new EnumsToStringConverter()));

        lock (ActiveEngines)
        {
            _logger.LogDebug("Adding script engine to active list {HashCode}", _nextEngineId);
            ActiveEngines.Add($"{GetHashCode()}-{_nextEngineId}", newEngine);
        }

        _scriptPluginConfigurationWrapper =
            new ScriptPluginConfigurationWrapper(_fileName.Split(Path.DirectorySeparatorChar).Last(), ScriptEngine,
                _configHandler);

        _scriptPluginConfigurationWrapper.ConfigurationUpdated += (configValue, callbackAction) =>
        {
            WrapJavaScriptErrorHandling(() =>
            {
                callbackAction.DynamicInvoke(JsValue.Undefined, new[] { configValue });
                return Task.CompletedTask;
            }, _logger, _fileName, _onProcessingScript);
        };
    }

    private void UnregisterScriptEntities(IManager manager)
    {
        foreach (var commandName in _registeredCommandNames)
        {
            manager.RemoveCommandByName(commandName);
            _logger.LogDebug("Unregistered script plugin command {Command} for {Plugin}", commandName, Name);
        }

        _registeredCommandNames.Clear();

        foreach (var interactionName in _registeredInteractions)
        {
            _interactionRegistration.UnregisterInteraction(interactionName);
        }

        _registeredInteractions.Clear();

        foreach (var (removeMethod, subscriptions) in _registeredEvents)
        {
            foreach (var subscription in subscriptions)
            {
                removeMethod.Invoke(null, new[] { subscription });
            }

            subscriptions.Clear();
        }

        _registeredEvents.Clear();
    }

    private void EventCallbackWrapper(string eventCallbackName, Delegate javascriptAction)
    {
        var eventCategory = eventCallbackName.Split(".")[0];

        var eventCategoryType = eventCategory switch
        {
            nameof(IManagementEventSubscriptions) => typeof(IManagementEventSubscriptions),
            nameof(IGameEventSubscriptions) => typeof(IGameEventSubscriptions),
            nameof(IGameServerEventSubscriptions) => typeof(IGameServerEventSubscriptions),
            _ => null
        };

        if (eventCategoryType is null)
        {
            _logger.LogWarning("{EventCategory} is not a valid subscription category", eventCategory);
            return;
        }

        var eventName = eventCallbackName.Split(".")[1];
        var eventAddMethod = eventCategoryType.GetMethods()
            .FirstOrDefault(method => method.Name.StartsWith($"add_{eventName}"));
        var eventRemoveMethod = eventCategoryType.GetMethods()
            .FirstOrDefault(method => method.Name.StartsWith($"remove_{eventName}"));

        if (eventAddMethod is null || eventRemoveMethod is null)
        {
            _logger.LogWarning("{EventName} is not a valid subscription event", eventName);
            return;
        }

        var genericType = eventAddMethod.GetParameters()[0].ParameterType.GetGenericArguments()[0];

        var eventWrapper =
            typeof(ScriptPluginV2).GetMethod(nameof(BuildEventWrapper), BindingFlags.Static | BindingFlags.NonPublic)!
                .MakeGenericMethod(genericType)
                .Invoke(null,
                    new object[]
                        { _logger, _fileName, javascriptAction, GetHashCode(), _nextEngineId, _onProcessingScript });

        eventAddMethod.Invoke(null, new[] { eventWrapper });

        if (!_registeredEvents.ContainsKey(eventRemoveMethod))
        {
            _registeredEvents.Add(eventRemoveMethod, new List<object> { eventWrapper });
        }
        else
        {
            _registeredEvents[eventRemoveMethod].Add(eventWrapper);
        }
    }

    private static Func<TEventType, CancellationToken, Task> BuildEventWrapper<TEventType>(ILogger logger,
        string fileName, Delegate javascriptAction, int hashCode, int engineId, SemaphoreSlim onProcessingScript)
    {
        return (coreEvent, token) =>
        {
            return WrapJavaScriptErrorHandling(() =>
                {
                    if (IsEngineDisposed(hashCode, engineId))
                    {
                        return Task.CompletedTask;
                    }

                    JavascriptEngine engine;

                    lock (ActiveEngines)
                    {
                        engine = ActiveEngines[$"{hashCode}-{engineId}"];
                    }

                    var args = new object[] { coreEvent, token }
                        .Select(param => JsValue.FromObject(engine, param))
                        .ToArray();
                    javascriptAction.DynamicInvoke(JsValue.Undefined, args);
                    return Task.CompletedTask;
                }, logger, fileName, onProcessingScript, (coreEvent as GameServerEvent)?.Server,
                additionalData: coreEvent.GetType().Name);
        };
    }

    private static bool IsEngineDisposed(int hashCode, int engineId)
    {
        lock (ActiveEngines)
        {
            return !ActiveEngines.ContainsKey($"{hashCode}-{engineId}");
        }
    }

    private static TResultType WrapJavaScriptErrorHandling<TResultType>(Func<TResultType> work, ILogger logger,
        string fileName, SemaphoreSlim onProcessingScript, IGameServer server = null, object additionalData = null,
        bool throwException = false,
        [CallerMemberName] string methodName = "")
    {
        using (LogContext.PushProperty("Server", server?.Id))
        {
            var waitCompleted = false;
            try
            {
                onProcessingScript.Wait();
                waitCompleted = true;
                return work();
            }
            catch (JavaScriptException ex)
            {
                logger.LogError(ex,
                    "Encountered JavaScript runtime error while executing {MethodName} for script plugin {Plugin} at {@LocationInfo} StackTrace={StackTrace} {@AdditionalData}",
                    methodName, Path.GetFileName(fileName), ex.Location, ex.StackTrace, additionalData);

                if (throwException)
                {
                    throw new PluginException("A runtime error occured while executing action for script plugin");
                }
            }
            catch (Exception ex) when (ex.InnerException is JavaScriptException jsEx)
            {
                logger.LogError(ex,
                    "Encountered JavaScript runtime error while executing {MethodName} for script plugin {Plugin} initialization {@LocationInfo} StackTrace={StackTrace} {@AdditionalData}",
                    methodName, fileName, jsEx.Location, jsEx.JavaScriptStackTrace, additionalData);

                if (throwException)
                {
                    throw new PluginException("A runtime error occured while executing action for script plugin");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Encountered JavaScript runtime error while executing {MethodName} for script plugin {Plugin}",
                    methodName, Path.GetFileName(fileName));

                if (throwException)
                {
                    throw new PluginException("An error occured while executing action for script plugin");
                }
            }
            finally
            {
                if (waitCompleted)
                {
                    onProcessingScript.Release(1);
                }
            }
        }

        return default;
    }

    private static ScriptPluginDetails AsScriptPluginInstance(dynamic source)
    {
        var commandDetails = ParseScriptCommandDetails(source);

        var interactionDetails = Array.Empty<ScriptPluginInteractionDetails>();
        if (HasProperty(source, "interactions") && source.interactions is dynamic[])
        {
            interactionDetails = ((dynamic[])source.interactions).Select(interaction =>
            {
                var name = HasProperty(interaction, "name") && interaction.name is string
                    ? (string)interaction.name
                    : string.Empty;
                var action = HasProperty(interaction, "action") && interaction.action is Delegate
                    ? (Delegate)interaction.action
                    : null;

                return new ScriptPluginInteractionDetails(name, action);
            }).ToArray();
        }

        var name = HasProperty(source, "name") && source.name is string ? (string)source.name : string.Empty;
        var author = HasProperty(source, "author") && source.author is string ? (string)source.author : string.Empty;
        var version = HasProperty(source, "version") && source.version is string ? (string)source.author : string.Empty;

        return new ScriptPluginDetails(name, author, version, commandDetails, interactionDetails);
    }

    private static ScriptPluginCommandDetails[] ParseScriptCommandDetails(dynamic source)
    {
        var commandDetails = Array.Empty<ScriptPluginCommandDetails>();
        if (HasProperty(source, "commands") && source.commands is dynamic[])
        {
            commandDetails = ((dynamic[])source.commands).Select(command =>
            {
                var commandArgs = Array.Empty<CommandArgument>();
                if (HasProperty(command, "arguments") && command.arguments is dynamic[])
                {
                    commandArgs = ((dynamic[])command.arguments).Select(argument => new CommandArgument
                    {
                        Name = HasProperty(argument, "name") ? argument.name : string.Empty,
                        Required = HasProperty(argument, "required") && argument.required is bool &&
                                   (bool)argument.required
                    }).ToArray();
                }

                var name = HasProperty(command, "name") && command.name is string
                    ? (string)command.name
                    : string.Empty;
                var description = HasProperty(command, "description") && command.description is string
                    ? (string)command.description
                    : string.Empty;
                var alias = HasProperty(command, "alias") && command.alias is string
                    ? (string)command.alias
                    : string.Empty;
                var permission = HasProperty(command, "permission") && command.permission is string
                    ? (string)command.permission
                    : string.Empty;
                var isTargetRequired = HasProperty(command, "targetRequired") && command.targetRequired is bool &&
                                       (bool)command.targetRequired;
                var supportedGames =
                    HasProperty(command, "supportedGames") && command.supportedGames is IEnumerable<object>
                        ? ((IEnumerable<object>)command.supportedGames).Where(game => !string.IsNullOrEmpty(game?.ToString()))
                        .Select(game =>
                            Enum.Parse<Reference.Game>(game.ToString()!))
                        : Array.Empty<Reference.Game>();
                var execute = HasProperty(command, "execute") && command.execute is Delegate
                    ? (Delegate)command.execute
                    : (GameEvent _) => Task.CompletedTask;

                return new ScriptPluginCommandDetails(name, description, alias, permission, isTargetRequired,
                    commandArgs, supportedGames, execute);
            }).ToArray();
        }

        return commandDetails;
    }

    private static bool HasProperty(dynamic source, string name)
    {
        Type objType = source.GetType();

        if (objType == typeof(ExpandoObject))
        {
            return ((IDictionary<string, object>)source).ContainsKey(name);
        }

        return objType.GetProperty(name) != null;
    }

    public class EnumsToStringConverter : IObjectConverter
    {
        public bool TryConvert(Engine engine, object value, out JsValue result)
        {
            if (value is Enum)
            {
                result = value.ToString();
                return true;
            }

            result = JsValue.Null;
            return false;
        }
    }
}
