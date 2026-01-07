using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using Microsoft.Extensions.Logging;

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

    // Cache for latest backups (invalidated every 30 seconds)
    private Dictionary<(Guid DeviceId, Guid ShareId), Backup>? _cachedLatestBackups;
    private DateTime _cacheTimestamp = DateTime.MinValue;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(30);
    private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

    public BackupRepository(
        IResticService resticService,
        IDeviceService deviceService,
        IShareService shareService,
        ILogger<BackupRepository> logger)
    {
        _resticService = resticService;
        _deviceService = deviceService;
        _shareService = shareService;
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
        await _cacheLock.WaitAsync();
        try
        {
            var isCacheValid = _cachedLatestBackups != null && 
                              DateTime.UtcNow - _cacheTimestamp < _cacheDuration;
            
            if (!isCacheValid)
            {
                _cacheLock.Release(); // Release before calling RebuildCacheAsync
                await RebuildCacheAsync();
                return;
            }
        }
        finally
        {
            if (_cacheLock.CurrentCount == 0)
            {
                _cacheLock.Release();
            }
        }
    }

    private async Task RebuildCacheAsync()
    {
        await _cacheLock.WaitAsync();
        try
        {
            _logger.LogInformation("Rebuilding backup cache...");
            
            // Fetch all backups from Restic
            var allBackups = await _resticService.ListBackups();
            
            // Group by (deviceId, shareId) and take the latest
            var latestBackups = allBackups
                .Where(b => b.ShareId.HasValue) // Only share-level backups
                .GroupBy(b => (DeviceId: b.DeviceId, ShareId: b.ShareId!.Value))
                .Select(g => new
                {
                    Key = g.Key,
                    Backup = g.OrderByDescending(b => b.Timestamp).First()
                })
                .ToDictionary(x => x.Key, x => x.Backup);
            
            _cachedLatestBackups = latestBackups;
            _cacheTimestamp = DateTime.UtcNow;
            
            _logger.LogInformation("Cache rebuilt with {Count} latest backups", latestBackups.Count);
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
