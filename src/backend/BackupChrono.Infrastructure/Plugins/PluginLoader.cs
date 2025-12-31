using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;

namespace BackupChrono.Infrastructure.Plugins;

/// <summary>
/// Plugin loader that manages protocol plugin registration and retrieval.
/// </summary>
public class PluginLoader
{
    private readonly Dictionary<ProtocolType, IProtocolPlugin> _plugins = new();

    public PluginLoader()
    {
        // Register built-in plugins
        RegisterPlugin(new SmbPlugin());
        RegisterPlugin(new SshPlugin());
        RegisterPlugin(new RsyncPlugin());
    }

    /// <summary>
    /// Registers a protocol plugin.
    /// </summary>
    public void RegisterPlugin(IProtocolPlugin plugin)
    {
        // Get protocol type by parsing plugin ProtocolName
        var protocolType = plugin.ProtocolName.ToUpperInvariant() switch
        {
            "SMB" => ProtocolType.SMB,
            "SSH" => ProtocolType.SSH,
            "RSYNC" => ProtocolType.Rsync,
            _ => throw new ArgumentException($"Unknown protocol: {plugin.ProtocolName}")
        };

        _plugins[protocolType] = plugin;
    }

    /// <summary>
    /// Gets a plugin for the specified protocol type.
    /// </summary>
    public IProtocolPlugin GetPlugin(ProtocolType protocolType)
    {
        if (!_plugins.TryGetValue(protocolType, out var plugin))
        {
            throw new NotSupportedException($"Protocol {protocolType} is not supported");
        }

        return plugin;
    }

    /// <summary>
    /// Gets all registered plugins.
    /// </summary>
    public IEnumerable<IProtocolPlugin> GetAllPlugins()
    {
        return _plugins.Values;
    }

    /// <summary>
    /// Checks if a protocol is supported.
    /// </summary>
    public bool IsProtocolSupported(ProtocolType protocolType)
    {
        return _plugins.ContainsKey(protocolType);
    }
}
