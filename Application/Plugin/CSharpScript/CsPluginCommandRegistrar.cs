using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Plugin.CSharpScript;

/// <summary>
/// Handles discovery, registration, and unregistration of commands from plugin assemblies.
/// </summary>
public class CsPluginCommandRegistrar(IServiceProvider rootServiceProvider, ILogger<CsPluginCommandRegistrar> logger)
{
    /// <summary>
    /// Discovers Command classes from the plugin assembly and registers them with the manager.
    /// </summary>
    public void RegisterCommands(CsPluginInstance instance, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(instance.PluginServiceProvider);

        try
        {
            var manager = rootServiceProvider.GetRequiredService<IManager>();

            var commandTypes = assembly.GetTypes()
                .Where(type =>
                    type.IsClass &&
                    !type.IsAbstract &&
                    type.IsSubclassOf(typeof(Command)) &&
                    (type.Namespace == null || !type.Namespace.StartsWith(nameof(SharedLibraryCore))))
                .ToList();

            foreach (var commandType in commandTypes)
            {
                try
                {
                    // Instantiate the command once
                    var command = (Command)ActivatorUtilities.CreateInstance(instance.PluginServiceProvider, commandType);

                    // Remove any existing commands that conflict by name or alias
                    RemoveConflictingCommands(manager, command.Name, command.Alias, commandType.Name, instance.FileName);

                    // Register the command
                    manager.AddAdditionalCommand(command);
                    instance.RegisteredCommands.Add(command);

                    logger.LogDebug("[{FileName}] Registered command: {CommandName} (alias: {Alias})",
                        instance.FileName, command.Name, command.Alias ?? "none");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[{FileName}] Failed to register command: {CommandType}",
                        instance.FileName, commandType.Name);
                }
            }

            if (instance.RegisteredCommands.Count > 0)
            {
                logger.LogDebug("[{FileName}] Registered {Count} command(s)",
                    instance.FileName, instance.RegisteredCommands.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{FileName}] Error discovering/registering commands", instance.FileName);
        }
    }

    /// <summary>
    /// Removes any registered commands that conflict with the given name or alias.
    /// </summary>
    private void RemoveConflictingCommands(
        IManager manager,
        string commandName,
        string? commandAlias,
        string typeName,
        string fileName)
    {
        var existingCommands = manager.GetCommands().Where(cmd =>
            cmd.Name.Equals(commandName, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(commandAlias) &&
             cmd.Alias?.Equals(commandAlias, StringComparison.OrdinalIgnoreCase) == true) ||
            (!string.IsNullOrEmpty(cmd.Alias) &&
             cmd.Alias.Equals(commandName, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(commandAlias) &&
             cmd.Name.Equals(commandAlias, StringComparison.OrdinalIgnoreCase)) ||
            cmd.GetType().Name == typeName).ToList();

        if (existingCommands.Count > 0)
        {
            logger.LogDebug("[{FileName}] Removing {Count} existing command(s) matching {CommandName}",
                fileName, existingCommands.Count, commandName);

            foreach (var existingCmd in existingCommands)
            {
                manager.RemoveCommandByName(existingCmd.Name);
            }
        }
    }

    /// <summary>
    /// Unregisters all commands that were registered by this plugin instance.
    /// </summary>
    public void UnregisterCommands(CsPluginInstance instance)
    {
        try
        {
            var manager = rootServiceProvider.GetRequiredService<IManager>();
            var commandCount = instance.RegisteredCommands.Count;

            if (commandCount == 0)
            {
                logger.LogDebug("[{FileName}] No commands to unregister", instance.FileName);
                return;
            }

            var unregisteredCount = 0;
            var commandsToRemove = instance.RegisteredCommands.ToList();

            foreach (var command in commandsToRemove)
            {
                try
                {
                    manager.RemoveCommandByName(command.Name);
                    unregisteredCount++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[{FileName}] Failed to unregister command: {CommandName}",
                        instance.FileName, command.Name);
                }
            }

            instance.RegisteredCommands.Clear();

            if (unregisteredCount > 0)
            {
                logger.LogDebug("[{FileName}] Unregistered {Count} command(s)", instance.FileName, unregisteredCount);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{FileName}] Error unregistering commands", instance.FileName);
        }
    }
}
