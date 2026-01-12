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
public class CsPluginCommandRegistrar(ILogger<CsPluginCommandRegistrar> logger, IManager manager)
{
    /// <summary>
    /// Discovers Command classes from the plugin assembly and registers them with the manager.
    /// </summary>
    public void RegisterCommands(CsPluginInstance instance, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(instance.PluginServiceProvider);

        try
        {
            var commandTypes = assembly.GetExportedTypes()
                .Where(type =>
                    type.IsClass &&
                    !type.IsAbstract &&
                    type.IsSubclassOf(typeof(Command)) &&
                    // Ensure we don't accidentally pick up base commands from the core library
                    (type.Namespace == null || !type.Namespace.StartsWith(nameof(SharedLibraryCore))))
                .ToList();

            foreach (var commandType in commandTypes)
            {
                try
                {
                    // Instantiate the command once
                    var command =
                        (Command)ActivatorUtilities.CreateInstance(instance.PluginServiceProvider, commandType);

                    // Remove any existing commands that conflict by name or alias
                    RemoveConflictingCommands(command, instance.FileName);

                    // Register the command
                    manager.AddAdditionalCommand(command);
                    instance.RegisteredCommands.Add(command);

                    logger.LogInformation("[{FileName}] Registered command: {CommandName} (alias: {Alias})",
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
    private void RemoveConflictingCommands(Command newCommand, string fileName)
    {
        var conflicts = manager.GetCommands()
            .Where(existing => IsConflict((Command)existing, newCommand))
            .ToList();

        if (conflicts.Count == 0)
            return;

        logger.LogWarning("[{FileName}] Overwriting {Count} existing command(s) due to conflict with {CommandName}",
            fileName, conflicts.Count, newCommand.Name);

        foreach (var conflict in conflicts)
        {
            manager.RemoveCommandByName(conflict.Name);
        }
    }

    /// <summary>
    /// Unregisters all commands that were registered by this plugin instance.
    /// </summary>
    public void UnregisterCommands(CsPluginInstance instance)
    {
        try
        {
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
                    logger.LogInformation("[{FileName}] Unregistering command: {CommandName} (alias: {Alias})",
                        instance.FileName, command.Name, command.Alias ?? "none");
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

    private static bool IsConflict(Command existing, Command newCmd)
    {
        // 1. Name vs Name
        if (existing.Name.Equals(newCmd.Name, StringComparison.OrdinalIgnoreCase))
            return true;

        // 2. Alias vs Alias (if both have one)
        if (!string.IsNullOrEmpty(existing.Alias) &&
            !string.IsNullOrEmpty(newCmd.Alias) &&
            existing.Alias.Equals(newCmd.Alias, StringComparison.OrdinalIgnoreCase))
            return true;

        // 3. Name vs Alias (New command name matches existing alias)
        if (!string.IsNullOrEmpty(existing.Alias) &&
            existing.Alias.Equals(newCmd.Name, StringComparison.OrdinalIgnoreCase))
            return true;

        // 4. Alias vs Name (New command alias matches existing name)
        return !string.IsNullOrEmpty(newCmd.Alias) &&
               existing.Name.Equals(newCmd.Alias, StringComparison.OrdinalIgnoreCase);
    }
}
