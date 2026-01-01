using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Infrastructure.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BackupChrono.Infrastructure.Protocols;

/// <summary>
/// Loader for protocol plugins. Instantiates and manages protocol plugin lifecycle.
/// </summary>
public class ProtocolPluginLoader : IProtocolPluginLoader
{
    private readonly Dictionary<ProtocolType, IProtocolPlugin> _plugins;
    private readonly ILogger<ProtocolPluginLoader> _logger;
    private readonly IServiceProvider _serviceProvider;

    public ProtocolPluginLoader(ILogger<ProtocolPluginLoader> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _plugins = new Dictionary<ProtocolType, IProtocolPlugin>();
        _serviceProvider = serviceProvider;
        
        // Initialize built-in plugins
        LoadBuiltInPlugins();
    }

    private void LoadBuiltInPlugins()
    {
        // Create instances of built-in plugins
        _plugins[ProtocolType.SMB] = _serviceProvider.GetRequiredService<SmbPlugin>();
        _plugins[ProtocolType.SSH] = _serviceProvider.GetRequiredService<SshPlugin>();
        _plugins[ProtocolType.Rsync] = _serviceProvider.GetRequiredService<RsyncPlugin>();

        
        _logger.LogInformation("Loaded {PluginCount} protocol plugins: {Protocols}", 
            _plugins.Count, 
            string.Join(", ", _plugins.Keys));
    }

    public IProtocolPlugin GetPlugin(ProtocolType protocol)
    {
        if (!_plugins.TryGetValue(protocol, out var plugin))
        {
            throw new NotSupportedException($"Protocol {protocol} is not supported.");
        }

        return plugin;
    }

    public IEnumerable<IProtocolPlugin> GetAllPlugins()
    {
        return _plugins.Values;
    }
}
