using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using BackupChrono.Infrastructure.Git;
using Microsoft.Extensions.Logging;

namespace BackupChrono.Infrastructure.Services;

/// <summary>
/// Service for managing device configurations with YAML persistence via Git.
/// </summary>
public class DeviceService : IDeviceService
{
    private readonly GitConfigService _gitConfigService;
    private readonly IProtocolPluginLoader _pluginLoader;
    private readonly ILogger<DeviceService> _logger;

    public DeviceService(
        GitConfigService gitConfigService,
        IProtocolPluginLoader pluginLoader,
        ILogger<DeviceService> logger)
    {
        _gitConfigService = gitConfigService;
        _pluginLoader = pluginLoader;
        _logger = logger;
    }

    public async Task<Device> CreateDevice(Device device)
    {
        ArgumentNullException.ThrowIfNull(device);

        // Validate device
        device.Validate();

        // Ensure unique name
        var existing = await GetDeviceByName(device.Name);
        if (existing != null)
        {
            throw new InvalidOperationException($"Device with name '{device.Name}' already exists.");
        }

        // Set timestamps (UpdatedAt is mutable, but we need to create a new instance for Id and CreatedAt)
        var now = DateTime.UtcNow;
        if (device.Id == Guid.Empty)
        {
            // Create new device with generated ID and timestamps
            device = new Device
            {
                Id = Guid.NewGuid(),
                Name = device.Name,
                Protocol = device.Protocol,
                Host = device.Host,
                Port = device.Port,
                Username = device.Username,
                Password = device.Password,
                WakeOnLanEnabled = device.WakeOnLanEnabled,
                WakeOnLanMacAddress = device.WakeOnLanMacAddress,
                Schedule = device.Schedule,
                RetentionPolicy = device.RetentionPolicy,
                IncludeExcludeRules = device.IncludeExcludeRules,
                CreatedAt = now,
                UpdatedAt = now
            };
        }
        else
        {
            device.UpdatedAt = now;
        }
        // Save and commit atomically to YAML/Git
        var filePath = GetDeviceFilePath(device.Name);
        await _gitConfigService.WriteAndCommitYamlFile(filePath, device, $"Add device: {device.Name}");

        return device;
    }

    public async Task<Device?> GetDevice(Guid id)
    {
        var devices = await ListDevices();
        return devices.FirstOrDefault(d => d.Id == id);
    }

    public async Task<Device?> GetDeviceByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var filePath = GetDeviceFilePath(name);
        var fullPath = Path.Combine(_gitConfigService.RepositoryPath, filePath);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        return await _gitConfigService.ReadYamlFile<Device>(filePath);
    }

    public async Task<IEnumerable<Device>> ListDevices()
    {
        var devicesDirectory = Path.Combine(_gitConfigService.RepositoryPath, "devices");
        if (!Directory.Exists(devicesDirectory))
        {
            return Enumerable.Empty<Device>();
        }

        var yamlFiles = Directory.GetFiles(devicesDirectory, "*.yaml", SearchOption.TopDirectoryOnly);
        var devices = new List<Device>();

        foreach (var file in yamlFiles)
        {
            try
            {
                var device = await _gitConfigService.ReadYamlFile<Device>(Path.GetRelativePath(_gitConfigService.RepositoryPath, file));
                if (device != null)
                {
                    devices.Add(device);
                }
            }
            catch (Exception ex)
            {
                // Log error but continue processing other files
                _logger.LogError(ex, "Failed to read device file {DeviceFile}", file);
            }
        }

        return devices;
    }

    public async Task<Device> UpdateDevice(Device device)
    {
        ArgumentNullException.ThrowIfNull(device);

        // Validate device
        device.Validate();

        // Ensure device exists
        var existing = await GetDevice(device.Id);
        if (existing == null)
        {
            throw new InvalidOperationException($"Device with ID '{device.Id}' not found.");
        }

        // Handle name change
        if (existing.Name != device.Name)
        {
            // Check if new name is already in use
            var nameExists = await GetDeviceByName(device.Name);
            if (nameExists != null && nameExists.Id != device.Id)
            {
                throw new InvalidOperationException($"Device with name '{device.Name}' already exists.");
            }
        }

        // Update timestamp
        device.UpdatedAt = DateTime.UtcNow;
        var filePath = GetDeviceFilePath(device.Name);
        var oldFilePath = existing.Name != device.Name ? GetDeviceFilePath(existing.Name) : null;

        // Stage new content
        await _gitConfigService.WriteYamlFile(filePath, device);

        // Stage deletion of the old file when renaming
        if (oldFilePath != null)
        {
            var fullOldPath = Path.Combine(_gitConfigService.RepositoryPath, oldFilePath);
            if (File.Exists(fullOldPath))
            {
                File.Delete(fullOldPath);
            }
        }

        var message = oldFilePath != null
            ? $"Update device: {existing.Name} -> {device.Name}"
            : $"Update device: {device.Name}";

        // Single commit containing both add/update and delete (if any)
        await _gitConfigService.CommitChanges(message);

        return device;
    }

    public async Task DeleteDevice(Guid id)
    {
        var device = await GetDevice(id);
        if (device == null)
        {
            throw new InvalidOperationException($"Device with ID '{id}' not found.");
        }

        // Delete device file
        var relativePath = GetDeviceFilePath(device.Name);
        var fullPath = Path.Combine(_gitConfigService.RepositoryPath, relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        // Delete associated shares for this device
        var sharesDirectory = Path.Combine(_gitConfigService.RepositoryPath, "shares", device.Name);        if (Directory.Exists(sharesDirectory))
        {
            Directory.Delete(sharesDirectory, recursive: true);
        }

        // Commit to Git
        await _gitConfigService.CommitChanges($"Delete device: {device.Name}");
    }

    public async Task<bool> TestConnection(Guid id)
    {
        var device = await GetDevice(id);
        if (device == null)
        {
            throw new InvalidOperationException($"Device with ID '{id}' not found.");
        }

        try
        {
            var plugin = _pluginLoader.GetPlugin(device.Protocol);
            return await plugin.TestConnection(device);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TestConnection failed for device {DeviceId}", device.Id);
            return false;
        }
    }

    private string GetDeviceFilePath(string deviceName)
    {
        if (deviceName.Contains("..") || deviceName.Contains(Path.DirectorySeparatorChar) ||
        deviceName.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("Invalid device name", nameof(deviceName));
        }
        return Path.Combine("devices", $"{deviceName}.yaml");
    }
}
