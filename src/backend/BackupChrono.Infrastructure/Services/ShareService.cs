using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Infrastructure.Git;
using Microsoft.Extensions.Logging;

namespace BackupChrono.Infrastructure.Services;

/// <summary>
/// Service for managing share configurations with YAML persistence via Git.
/// </summary>
public class ShareService : IShareService
{
    private readonly GitConfigService _gitConfigService;
    private readonly IDeviceService _deviceService;
    private readonly ILogger<ShareService> _logger;

    public ShareService(GitConfigService gitConfigService, IDeviceService deviceService, ILogger<ShareService> logger)
    {
        _gitConfigService = gitConfigService;
        _deviceService = deviceService;
        _logger = logger;
    }

    public async Task<Share> CreateShare(Share share)
    {
        ArgumentNullException.ThrowIfNull(share);

        // Ensure device exists
        var device = await _deviceService.GetDevice(share.DeviceId);
        if (device == null)
        {
            throw new InvalidOperationException($"Device with ID '{share.DeviceId}' not found.");
        }

        // Ensure unique name within device
        var existingShares = await ListShares(share.DeviceId);
        if (existingShares.Any(s => s.Name == share.Name))
        {
            throw new InvalidOperationException($"Share with name '{share.Name}' already exists for device '{device.Name}'.");
        }

        // Create new share with generated ID and timestamps if needed
        var now = DateTime.UtcNow;
        if (share.Id == Guid.Empty)
        {
            share = new Share
            {
                Id = Guid.NewGuid(),
                DeviceId = share.DeviceId,
                Name = share.Name,
                Path = share.Path,
                Enabled = share.Enabled,
                Schedule = share.Schedule,
                RetentionPolicy = share.RetentionPolicy,
                IncludeExcludeRules = share.IncludeExcludeRules,
                CreatedAt = now,
                UpdatedAt = now
            };
        }
        else
        {
            share.UpdatedAt = now;
        }

        // Save and commit atomically to YAML/Git
        var filePath = GetShareFilePath(device.Name, share.Name);
        await _gitConfigService.WriteAndCommitYamlFile(filePath, share, $"Add share: {device.Name}/{share.Name}");

        return share;
    }

    public async Task<Share?> GetShare(Guid id)
    {
        var shares = await ListAllShares();
        return shares.FirstOrDefault(s => s.Id == id);
    }

    public async Task<IEnumerable<Share>> ListShares(Guid deviceId)
    {
        var device = await _deviceService.GetDevice(deviceId);
        if (device == null)
        {
            return Enumerable.Empty<Share>();
        }

        var sharesDirectory = Path.Combine(_gitConfigService.RepositoryPath, "shares", device.Name);
        if (!Directory.Exists(sharesDirectory))
        {
            return Enumerable.Empty<Share>();
        }

        var yamlFiles = Directory.GetFiles(sharesDirectory, "*.yaml", SearchOption.TopDirectoryOnly);
        var shares = new List<Share>();

        foreach (var file in yamlFiles)
        {
            try
            {
                var share = await _gitConfigService.ReadYamlFile<Share>(Path.GetRelativePath(_gitConfigService.RepositoryPath, file));
                if (share != null)
                {
                    shares.Add(share);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read share file {ShareFile}", file);
            }
        }

        return shares;
    }

    public async Task<IEnumerable<Share>> ListAllShares()
    {
        var sharesDirectory = Path.Combine(_gitConfigService.RepositoryPath, "shares");
        if (!Directory.Exists(sharesDirectory))
        {
            return Enumerable.Empty<Share>();
        }

        var shares = new List<Share>();
        var deviceDirectories = Directory.GetDirectories(sharesDirectory);

        foreach (var deviceDir in deviceDirectories)
        {
            var yamlFiles = Directory.GetFiles(deviceDir, "*.yaml", SearchOption.TopDirectoryOnly);
            foreach (var file in yamlFiles)
            {
                try
                {
                    var share = await _gitConfigService.ReadYamlFile<Share>(Path.GetRelativePath(_gitConfigService.RepositoryPath, file));
                    if (share != null)
                    {
                        shares.Add(share);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to read share file {ShareFile}", file);
                }
            }
        }

        return shares;
    }

    public async Task<Share> UpdateShare(Share share)
    {
        ArgumentNullException.ThrowIfNull(share);

        // Ensure share exists
        var existing = await GetShare(share.Id);
        if (existing == null)
        {
            throw new InvalidOperationException($"Share with ID '{share.Id}' not found.");
        }

        // Ensure device exists
        var device = await _deviceService.GetDevice(share.DeviceId);
        if (device == null)
        {
            throw new InvalidOperationException($"Device with ID '{share.DeviceId}' not found.");
        }

        // Get old device for path changes
        var oldDevice = await _deviceService.GetDevice(existing.DeviceId);
        if (oldDevice == null)
        {
            throw new InvalidOperationException($"Original device with ID '{existing.DeviceId}' not found.");
        }

        // Handle name or device change
        var nameOrDeviceChanged = existing.Name != share.Name || existing.DeviceId != share.DeviceId;
        string? oldFilePath = null;
        string? fullOldPath = null;

        if (nameOrDeviceChanged)
        {
            // Check if new name is already in use for the target device
            var sharesForDevice = await ListShares(share.DeviceId);
            if (sharesForDevice.Any(s => s.Name == share.Name && s.Id != share.Id))
            {
                throw new InvalidOperationException($"Share with name '{share.Name}' already exists for device '{device.Name}'.");
            }

            oldFilePath = GetShareFilePath(oldDevice.Name, existing.Name);
            fullOldPath = Path.Combine(_gitConfigService.RepositoryPath, oldFilePath);
        }

        // Update timestamp
        share.UpdatedAt = DateTime.UtcNow;

        var filePath = GetShareFilePath(device.Name, share.Name);

        // Delete old file after validations when path changed (before commit)
        if (nameOrDeviceChanged && fullOldPath != null && !string.Equals(fullOldPath, Path.Combine(_gitConfigService.RepositoryPath, filePath), StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(fullOldPath))
            {
                File.Delete(fullOldPath);
            }
        }

        // Commit to Git atomically with the write above
        var message = existing.DeviceId != share.DeviceId || existing.Name != share.Name
            ? $"Update share: {oldDevice.Name}/{existing.Name} → {device.Name}/{share.Name}"
            : $"Update share: {device.Name}/{share.Name}";
        await _gitConfigService.WriteAndCommitYamlFile(filePath, share, message);

        return share;
    }

    public async Task DeleteShare(Guid id)
    {
        var share = await GetShare(id);
        if (share == null)
        {
            throw new InvalidOperationException($"Share with ID '{id}' not found.");
        }

        var device = await _deviceService.GetDevice(share.DeviceId);
        if (device == null)
        {
            throw new InvalidOperationException($"Device with ID '{share.DeviceId}' not found.");
        }

        // Delete share file
        var filePath = GetShareFilePath(device.Name, share.Name);
        var fullPath = Path.Combine(_gitConfigService.RepositoryPath, filePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        // Commit to Git
        await _gitConfigService.CommitChanges($"Delete share: {device.Name}/{share.Name}");
    }

    public async Task SetShareEnabled(Guid id, bool enabled)
    {
        var share = await GetShare(id);
        if (share == null)
        {
            throw new InvalidOperationException($"Share with ID '{id}' not found.");
        }

        if (share.Enabled == enabled)
        {
            return; // No change needed
        }

        // Create updated share with new enabled status
        var updatedShare = new Share
        {
            Id = share.Id,
            DeviceId = share.DeviceId,
            Name = share.Name,
            Path = share.Path,
            Enabled = enabled,
            Schedule = share.Schedule,
            RetentionPolicy = share.RetentionPolicy,
            IncludeExcludeRules = share.IncludeExcludeRules,
            CreatedAt = share.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };

        await UpdateShare(updatedShare);
    }

    private string GetShareFilePath(string deviceName, string shareName)
    {
        // Sanitize inputs to prevent path traversal
        if (deviceName.Contains("..") || deviceName.Contains(Path.DirectorySeparatorChar) || 
            deviceName.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("Invalid device name", nameof(deviceName));
        }
        if (shareName.Contains("..") || shareName.Contains(Path.DirectorySeparatorChar) || 
            shareName.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("Invalid share name", nameof(shareName));
        }
        
        return Path.Combine("shares", deviceName, $"{shareName}.yaml");
    }}
