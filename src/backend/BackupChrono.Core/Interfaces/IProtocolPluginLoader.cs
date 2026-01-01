using BackupChrono.Core.Entities;

namespace BackupChrono.Core.Interfaces;

/// <summary>
/// Service for loading and managing protocol plugins.
/// </summary>
public interface IProtocolPluginLoader
{
    /// <summary>
    /// Gets a protocol plugin for the specified protocol type.
    /// </summary>
    IProtocolPlugin GetPlugin(ProtocolType protocol);

    /// <summary>
    /// Gets all available protocol plugins.
    /// </summary>
    IEnumerable<IProtocolPlugin> GetAllPlugins();
}
