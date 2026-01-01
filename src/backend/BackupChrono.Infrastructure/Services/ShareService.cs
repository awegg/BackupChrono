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
        share = new Share
        {
            Id = share.Id == Guid.Empty ? Guid.NewGuid() : share.Id,
            DeviceId = share.DeviceId,
            Name = share.Name,
            Path = share.Path,
            Enabled = share.Enabled,
            Schedule = share.Schedule,
            RetentionPolicy = share.RetentionPolicy,
            IncludeExcludeRules = share.IncludeExcludeRules,
            RepositoryPassword = share.RepositoryPassword,
            RepositoryKeySalt = share.RepositoryKeySalt,
            CreatedAt = share.Id == Guid.Empty ? now : share.CreatedAt,
            UpdatedAt = now
        };

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

        // Preserve existing repo secrets if caller did not provide them
        share.RepositoryPassword ??= existing.RepositoryPassword;
        share.RepositoryKeySalt ??= existing.RepositoryKeySalt;

        // Update timestamp
        share.UpdatedAt = DateTime.UtcNow;

        var filePath = GetShareFilePath(device.Name, share.Name);

        // Stage new content first
        await _gitConfigService.WriteYamlFile(filePath, share);

        // Stage deletion of the old file after the new content exists
        if (nameOrDeviceChanged && fullOldPath != null && !string.Equals(fullOldPath, Path.Combine(_gitConfigService.RepositoryPath, filePath), StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(fullOldPath))
            {
                File.Delete(fullOldPath);
            }
        }

        var message = existing.DeviceId != share.DeviceId || existing.Name != share.Name
            ? $"Update share: {oldDevice.Name}/{existing.Name} -> {device.Name}/{share.Name}"
            : $"Update share: {device.Name}/{share.Name}";

        // Single commit containing both add/update and delete (if any)
        await _gitConfigService.CommitChanges(message);

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
        // Comprehensive path traversal validation
        ValidatePathComponent(deviceName, nameof(deviceName));
        ValidatePathComponent(shareName, nameof(shareName));
        
        return Path.Combine("shares", deviceName, $"{shareName}.yaml");
    }

    private static void ValidatePathComponent(string component, string paramName)
    {
        if (string.IsNullOrWhiteSpace(component))
        {
            throw new ArgumentException("Path component cannot be empty", paramName);
        }

        // Check for path traversal
        if (component.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("Path traversal detected", paramName);
        }

        // Check for path separators
        if (component.Contains(Path.DirectorySeparatorChar) || 
            component.Contains(Path.AltDirectorySeparatorChar) ||
            component.Contains('/'))
        {
            throw new ArgumentException("Path separators not allowed", paramName);
        }

        // Check for null bytes
        if (component.Contains('\0'))
        {
            throw new ArgumentException("Null bytes not allowed", paramName);
        }

        // Check for leading/trailing whitespace or dots
        if (component != component.Trim() || component.StartsWith('.') || component.EndsWith('.'))
        {
            throw new ArgumentException("Invalid leading/trailing characters", paramName);
        }

        // Check for reserved Windows names (CON, PRN, AUX, NUL, COM1-9, LPT1-9)
        var upperName = component.ToUpperInvariant();
        string[] reservedNames = { "CON", "PRN", "AUX", "NUL" };
        if (reservedNames.Contains(upperName) || 
            (upperName.Length == 4 && (upperName.StartsWith("COM") || upperName.StartsWith("LPT")) && char.IsDigit(upperName[3])))
        {
            throw new ArgumentException("Reserved system name not allowed", paramName);
        }
    }
}
