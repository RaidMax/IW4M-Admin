using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IW4MAdmin.Application.Plugin.CSharpScript;

/// <summary>
/// A service provider that:
/// 1. First tries to instantiate services registered by the plugin (using root provider for their dependencies)
/// 2. Falls back to the root provider for all other services
/// </summary>
internal class PluginScopedServiceProvider : IServiceProvider, IDisposable
{
    private readonly Dictionary<Type, ServiceDescriptor> _pluginServiceDescriptors;
    private readonly Dictionary<Type, object> _resolvedServices = new();
    private readonly IServiceProvider _rootProvider;
    private readonly ILogger _logger;

    public PluginScopedServiceProvider(
        IServiceCollection pluginServices,
        IServiceProvider rootProvider,
        ILogger logger)
    {
        _rootProvider = rootProvider;
        _logger = logger;

        // Index the plugin's service descriptors by service type
        _pluginServiceDescriptors = pluginServices
            .ToDictionary(sd => sd.ServiceType, sd => sd);
    }

    public object? GetService(Type serviceType)
    {
        // Check if this is a plugin-registered service
        if (_pluginServiceDescriptors.TryGetValue(serviceType, out var descriptor))
        {
            return ResolvePluginService(serviceType, descriptor);
        }

        // Fall back to root provider
        return _rootProvider.GetService(serviceType);
    }

    private object? ResolvePluginService(Type serviceType, ServiceDescriptor descriptor)
    {
        // Check if already resolved (for singletons)
        if (descriptor.Lifetime == ServiceLifetime.Singleton &&
            _resolvedServices.TryGetValue(serviceType, out var existing))
        {
            return existing;
        }

        object? instance = null;

        try
        {
            if (descriptor.ImplementationInstance != null)
            {
                // Pre-created instance
                instance = descriptor.ImplementationInstance;
            }
            else if (descriptor.ImplementationFactory != null)
            {
                // Factory - pass this provider so it can resolve dependencies
                instance = descriptor.ImplementationFactory(this);
            }
            else if (descriptor.ImplementationType != null)
            {
                // Create instance using ActivatorUtilities - this will use the root provider
                // for any dependencies the service needs
                instance = ActivatorUtilities.CreateInstance(_rootProvider, descriptor.ImplementationType);
            }

            // Cache singletons
            if (instance != null && descriptor.Lifetime == ServiceLifetime.Singleton)
            {
                _resolvedServices[serviceType] = instance;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve plugin service {ServiceType}", serviceType.Name);
        }

        return instance;
    }

    public void Dispose()
    {
        foreach (var service in _resolvedServices.Values)
        {
            if (service is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _resolvedServices.Clear();
    }
}