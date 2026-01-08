using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BackupChrono.Infrastructure.Repositories;

/// <summary>
/// Repository for querying and aggregating backup data from Restic.
/// Implements caching and batch optimization for performance.
/// </summary>
public class BackupRepository : IBackupRepository
{
    private readonly IResticService _resticService;
    private readonly IDeviceService _deviceService;
    private readonly IShareService _shareService;
    private readonly ILogger<BackupRepository> _logger;
    private readonly string _repositoryBasePath;

    // Cache for latest backups (invalidated every 30 seconds)
    private Dictionary<(Guid DeviceId, Guid ShareId), Backup>? _cachedLatestBackups;
    private DateTime _cacheTimestamp = DateTime.MinValue;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(30);
    private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

    public BackupRepository(
        IResticService resticService,
        IDeviceService deviceService,
        IShareService shareService,
        IOptions<ResticOptions> resticOptions,
        ILogger<BackupRepository> logger)
    {
        _resticService = resticService;
        _deviceService = deviceService;
        _shareService = shareService;
        _repositoryBasePath = resticOptions.Value.RepositoryBasePath;
        _logger = logger;
    }

    public async Task<Backup?> GetLatestBackup(Guid deviceId, Guid shareId)
    {
        try
        {
            // Check cache first
            await _cacheLock.WaitAsync();
            try
            {
                if (_cachedLatestBackups != null && 
                    DateTime.UtcNow - _cacheTimestamp < _cacheDuration)
                {
                    if (_cachedLatestBackups.TryGetValue((deviceId, shareId), out var cachedBackup))
                    {
                        _logger.LogDebug("Cache hit for backup: deviceId={DeviceId}, shareId={ShareId}", 
                            deviceId, shareId);
                        return cachedBackup;
                    }
                }
            }
            finally
            {
                _cacheLock.Release();
            }

            // Cache miss or expired - rebuild cache
            await RebuildCacheAsync();

            // Return from fresh cache
            await _cacheLock.WaitAsync();
            try
            {
                if (_cachedLatestBackups != null &&
                    _cachedLatestBackups.TryGetValue((deviceId, shareId), out var backup))
                {
                    return backup;
                }
            }
            finally
            {
                _cacheLock.Release();
            }

            _logger.LogDebug("No backup found for deviceId={DeviceId}, shareId={ShareId}", 
                deviceId, shareId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest backup for deviceId={DeviceId}, shareId={ShareId}", 
                deviceId, shareId);
            return null;
        }
    }

    public async Task<Dictionary<Guid, Backup>> GetLatestBackupsForShares(List<Guid> shareIds)
    {
        try
        {
            // Rebuild cache if needed
            await EnsureCacheIsValidAsync();

            // Extract results for requested shares
            var result = new Dictionary<Guid, Backup>();
            
            await _cacheLock.WaitAsync();
            try
            {
                if (_cachedLatestBackups != null)
                {
                    foreach (var shareId in shareIds)
                    {
                        var entry = _cachedLatestBackups
                            .FirstOrDefault(kvp => kvp.Key.ShareId == shareId);
                        
                        if (entry.Value != null)
                        {
                            result[shareId] = entry.Value;
                        }
                    }
                }
            }
            finally
            {
                _cacheLock.Release();
            }

            _logger.LogDebug("Batch query returned {Count} backups for {RequestedCount} shares", 
                result.Count, shareIds.Count);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest backups for {Count} shares", shareIds.Count);
            return new Dictionary<Guid, Backup>();
        }
    }

    public async Task<OverviewStatistics> GetOverviewStatistics()
    {
        try
        {
            await EnsureCacheIsValidAsync();

            var devices = await _deviceService.ListDevices();
            var twoDaysAgo = DateTime.UtcNow.AddDays(-2);
            
            var stats = new OverviewStatistics
            {
                TotalDevices = devices.Count()
            };

            await _cacheLock.WaitAsync();
            try
            {
                if (_cachedLatestBackups != null)
                {
                    stats.TotalShares = _cachedLatestBackups.Count;
                    stats.TotalFiles = _cachedLatestBackups.Values.Sum(b => 
                        b.FilesNew + b.FilesChanged + b.FilesUnmodified);
                    stats.TotalProtectedBytes = _cachedLatestBackups.Values.Sum(b => 
                        b.DataAdded);
                    
                    // Count devices with failures
                    var devicesWithFailures = _cachedLatestBackups
                        .Where(kvp => kvp.Value.Status == BackupStatus.Failed)
                        .Select(kvp => kvp.Key.DeviceId)
                        .Distinct()
                        .Count();
                    stats.DevicesWithFailures = devicesWithFailures;
                    
                    // Count devices with stale backups
                    var devicesWithStaleBackups = _cachedLatestBackups
                        .Where(kvp => kvp.Value.Timestamp < twoDaysAgo)
                        .Select(kvp => kvp.Key.DeviceId)
                        .Distinct()
                        .Count();
                    stats.DevicesWithStaleBackups = devicesWithStaleBackups;
                }
            }
            finally
            {
                _cacheLock.Release();
            }

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting overview statistics");
            return new OverviewStatistics();
        }
    }

    private async Task EnsureCacheIsValidAsync()
    {
        bool needsRebuild = false;
        
        await _cacheLock.WaitAsync();
        try
        {
            var isCacheValid = _cachedLatestBackups != null && 
                              DateTime.UtcNow - _cacheTimestamp < _cacheDuration;
            
            needsRebuild = !isCacheValid;
        }
        finally
        {
            _cacheLock.Release();
        }
        
        if (needsRebuild)
        {
            await RebuildCacheAsync();
        }
    }

    private async Task RebuildCacheAsync()
    {
        await _cacheLock.WaitAsync();
        try
        {
            _logger.LogInformation("Rebuilding backup cache...");
            
            var latestBackups = new Dictionary<(Guid DeviceId, Guid ShareId), Backup>();
            
            // Fetch backups for each device
            var devices = await _deviceService.ListDevices();
            
            foreach (var device in devices)
            {
                try
                {
                    var shares = await _shareService.ListShares(device.Id);
                    var sharesList = shares.ToList();
                    
                    // Process each share's repository
                    foreach (var share in sharesList)
                    {
                        try
                        {
                            // Repository path is {RepositoryBasePath}/{deviceId}/{shareId}
                            var repositoryPath = Path.Combine(_repositoryBasePath, device.Id.ToString(), share.Id.ToString());
                            
                            // List all backups for this specific share repository
                            // Don't filter by hostname - the repository path is already device+share specific
                            var backups = await _resticService.ListBackups(null, repositoryPath);
                            
                            if (backups.Any())
                            {
                                // Get the latest backup for this share
                                var latestBackup = backups.OrderByDescending(b => b.Timestamp).First();
                                latestBackups[(device.Id, share.Id)] = latestBackup;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error listing backups for device {DeviceId} share {ShareId}", 
                                device.Id, share.Id);
                            // Continue with other shares
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing backups for device {DeviceId}", device.Id);
                    // Continue with other devices
                }
            }
            
            _cachedLatestBackups = latestBackups;
            _cacheTimestamp = DateTime.UtcNow;
            
            var shareLevelCount = latestBackups.Values.Count(b => b.ShareId.HasValue);
            _logger.LogInformation("Cache rebuilt with {Count} latest backups ({ShareLevel} share-level, {DeviceLevel} from device-level)", 
                latestBackups.Count, 
                shareLevelCount,
                latestBackups.Count - shareLevelCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rebuilding backup cache");
            _cachedLatestBackups = new Dictionary<(Guid, Guid), Backup>();
        }
        finally
        {
            _cacheLock.Release();
        }
    }
}
