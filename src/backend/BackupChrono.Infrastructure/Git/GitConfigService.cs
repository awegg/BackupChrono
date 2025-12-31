using BackupChrono.Core.Entities;
using BackupChrono.Core.ValueObjects;
using LibGit2Sharp;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace BackupChrono.Infrastructure.Git;

/// <summary>
/// Service for managing configuration files in a Git repository with YAML serialization.
/// </summary>
public class GitConfigService
{
    private readonly string _repositoryPath;
    private readonly ISerializer _yamlSerializer;
    private readonly IDeserializer _yamlDeserializer;
    private static readonly SemaphoreSlim _gitLock = new SemaphoreSlim(1, 1);

    public GitConfigService(string repositoryPath)
    {
        _repositoryPath = repositoryPath;
        
        _yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithTypeConverter(new YamlEncryptedCredentialConverter())
            .Build();
            
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithTypeConverter(new YamlEncryptedCredentialConverter())
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Initializes a new Git repository if it doesn't exist.
    /// </summary>
    public void InitializeRepository()
    {
        if (!Directory.Exists(_repositoryPath))
        {
            Directory.CreateDirectory(_repositoryPath);
        }

        if (!LibGit2Sharp.Repository.IsValid(_repositoryPath))
        {
            LibGit2Sharp.Repository.Init(_repositoryPath);
        }
    }

    /// <summary>
    /// Saves a device configuration to a YAML file and commits it to Git.
    /// </summary>
    public async Task SaveDevice(Device device)
    {
        var devicePath = Path.Combine(_repositoryPath, "devices", $"{device.Name}.yaml");
        var yaml = _yamlSerializer.Serialize(device);

        await _gitLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(devicePath)!);
            await File.WriteAllTextAsync(devicePath, yaml);
            await CommitChangesAsync($"Update device: {device.Name}", new[] { devicePath });
        }
        finally
        {
            _gitLock.Release();
        }
    }

    /// <summary>
    /// Loads a device configuration from a YAML file.
    /// </summary>
    public async Task<Device?> LoadDevice(string deviceName)
    {
        var devicePath = Path.Combine(_repositoryPath, "devices", $"{deviceName}.yaml");
        
        if (!File.Exists(devicePath))
            return null;

        var yaml = await File.ReadAllTextAsync(devicePath);
        return _yamlDeserializer.Deserialize<Device>(yaml);
    }

    /// <summary>
    /// Lists all device configurations.
    /// </summary>
    public async Task<IEnumerable<Device>> ListDevices()
    {
        var devicesPath = Path.Combine(_repositoryPath, "devices");
        
        if (!Directory.Exists(devicesPath))
            return Enumerable.Empty<Device>();

        var devices = new List<Device>();
        foreach (var file in Directory.GetFiles(devicesPath, "*.yaml"))
        {
            var yaml = await File.ReadAllTextAsync(file);
            var device = _yamlDeserializer.Deserialize<Device>(yaml);
            if (device != null)
                devices.Add(device);
        }

        return devices;
    }

    /// <summary>
    /// Deletes a device configuration and commits the change.
    /// </summary>
    public async Task DeleteDevice(string deviceName)
    {
        var devicePath = Path.Combine(_repositoryPath, "devices", $"{deviceName}.yaml");
        
        await _gitLock.WaitAsync();
        try
        {
            if (File.Exists(devicePath))
            {
                File.Delete(devicePath);
                await CommitChangesAsync($"Delete device: {deviceName}", new[] { devicePath });
            }
        }
        finally
        {
            _gitLock.Release();
        }
    }

    /// <summary>
    /// Saves a share configuration to a YAML file and commits it to Git.
    /// </summary>
    public async Task SaveShare(Share share, string deviceName)
    {
        var sharePath = Path.Combine(_repositoryPath, "shares", deviceName, $"{share.Name}.yaml");
        var yaml = _yamlSerializer.Serialize(share);

        await _gitLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(sharePath)!);
            await File.WriteAllTextAsync(sharePath, yaml);
            await CommitChangesAsync($"Update share: {deviceName}/{share.Name}", new[] { sharePath });
        }
        finally
        {
            _gitLock.Release();
        }
    }

    /// <summary>
    /// Loads a share configuration from a YAML file.
    /// </summary>
    public async Task<Share?> LoadShare(string deviceName, string shareName)
    {
        var sharePath = Path.Combine(_repositoryPath, "shares", deviceName, $"{shareName}.yaml");
        
        if (!File.Exists(sharePath))
            return null;

        var yaml = await File.ReadAllTextAsync(sharePath);
        return _yamlDeserializer.Deserialize<Share>(yaml);
    }

    /// <summary>
    /// Lists all shares for a device.
    /// </summary>
    public async Task<IEnumerable<Share>> ListShares(string deviceName)
    {
        var sharesPath = Path.Combine(_repositoryPath, "shares", deviceName);
        
        if (!Directory.Exists(sharesPath))
            return Enumerable.Empty<Share>();

        var shares = new List<Share>();
        foreach (var file in Directory.GetFiles(sharesPath, "*.yaml"))
        {
            var yaml = await File.ReadAllTextAsync(file);
            var share = _yamlDeserializer.Deserialize<Share>(yaml);
            if (share != null)
                shares.Add(share);
        }

        return shares;
    }

    /// <summary>
    /// Deletes a share configuration and commits the change.
    /// </summary>
    public async Task DeleteShare(string deviceName, string shareName)
    {
        var sharePath = Path.Combine(_repositoryPath, "shares", deviceName, $"{shareName}.yaml");
        
        await _gitLock.WaitAsync();
        try
        {
            if (File.Exists(sharePath))
            {
                File.Delete(sharePath);
                await CommitChangesAsync($"Delete share: {deviceName}/{shareName}", new[] { sharePath });
            }
        }
        finally
        {
            _gitLock.Release();
        }
    }

    /// <summary>
    /// Gets the Git commit history for configuration changes.
    /// </summary>
    public IEnumerable<ConfigurationCommit> GetCommitHistory(int maxCount = 100)
    {
        using var repo = new LibGit2Sharp.Repository(_repositoryPath);
        
        return repo.Commits
            .Take(maxCount)
            .Select(c => new ConfigurationCommit
            {
                Hash = c.Sha,
                Timestamp = c.Author.When.UtcDateTime,
                Author = c.Author.Name,
                Email = c.Author.Email,
                Message = c.MessageShort,
                FilesChanged = c.Parents.Any() 
                    ? repo.Diff.Compare<TreeChanges>(c.Parents.First().Tree, c.Tree)
                        .Select(change => change.Path).ToArray()
                    : Array.Empty<string>()
            })
            .ToList();
    }

    /// <summary>
    /// Commits changes to the Git repository.
    /// </summary>
    private async Task CommitChangesAsync(string message, string[] files)
    {
        await Task.Run(() =>
        {
            using var repo = new LibGit2Sharp.Repository(_repositoryPath);
            
            Commands.Stage(repo, files);

            var signature = new Signature("BackupChrono", "backupchrono@system", DateTimeOffset.Now);
            repo.Commit(message, signature, signature);
        });
    }
}
