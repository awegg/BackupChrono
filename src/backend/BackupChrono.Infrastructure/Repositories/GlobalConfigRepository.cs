using BackupChrono.Core.Interfaces;
using BackupChrono.Core.ValueObjects;
using BackupChrono.Infrastructure.Git;
using Microsoft.Extensions.Logging;

namespace BackupChrono.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for global configuration using GitConfigService.
/// </summary>
public class GlobalConfigRepository : IGlobalConfigRepository
{
    private readonly GitConfigService _gitConfigService;
    private readonly ILogger<GlobalConfigRepository> _logger;

    public GlobalConfigRepository(GitConfigService gitConfigService, ILogger<GlobalConfigRepository> logger)
    {
        _gitConfigService = gitConfigService;
        _logger = logger;
    }

    public async Task<RetentionPolicy?> GetDefaultRetentionPolicy()
    {
        try
        {
            var globalConfig = await _gitConfigService.ReadYamlFile<GlobalConfig>("global.yaml");
            return globalConfig?.DefaultRetentionPolicy;
        }
        catch (FileNotFoundException)
        {
            _logger.LogWarning("global.yaml not found; no default retention policy available");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read global retention policy from global.yaml");
            return null;
        }
    }

    public async Task<T?> GetConfiguration<T>(string key) where T : class
    {
        try
        {
            return await _gitConfigService.ReadYamlFile<T>($"{key}.yaml");
        }
        catch (FileNotFoundException)
        {
            _logger.LogDebug("Configuration file {Key}.yaml not found", key);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read configuration {Key}", key);
            return null;
        }
    }

    public async Task SaveConfiguration<T>(string key, T value) where T : class
    {
        try
        {
            await _gitConfigService.WriteYamlFile($"{key}.yaml", value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration for key: {Key}", key);
            throw new InvalidOperationException($"Failed to save configuration for {key}", ex);
        }
    }

    /// <summary>
    /// Internal class for reading global.yaml structure.
    /// </summary>
    private class GlobalConfig
    {
        public RetentionPolicy? DefaultRetentionPolicy { get; set; }
    }
}
