using BackupChrono.Core.ValueObjects;

namespace BackupChrono.Core.Interfaces;

/// <summary>
/// Repository for accessing global configuration settings.
/// </summary>
public interface IGlobalConfigRepository
{
    /// <summary>
    /// Gets the global default retention policy.
    /// </summary>
    Task<RetentionPolicy?> GetDefaultRetentionPolicy();
    
    /// <summary>
    /// Gets a global configuration value by key.
    /// </summary>
    Task<T?> GetConfiguration<T>(string key) where T : class;
    
    /// <summary>
    /// Saves a global configuration value.
    /// </summary>
    Task SaveConfiguration<T>(string key, T value) where T : class;
}
