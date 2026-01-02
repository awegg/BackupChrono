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

    public string RepositoryPath => _repositoryPath;

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
        
        // Auto-initialize Git repository if it doesn't exist
        InitializeRepository();
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
    /// Writes an object to a YAML file (relative to repository root).
    /// Acquires the Git lock to ensure thread-safe file writes when used with CommitChanges.
    /// </summary>
    public async Task WriteAndCommitYamlFile<T>(string relativePath, T data, string commitMessage)
    {
        var fullPath = Path.Combine(_repositoryPath, relativePath);
        var yaml = _yamlSerializer.Serialize(data);

        await _gitLock.WaitAsync();
        try
        {
            var directory = Path.GetDirectoryName(fullPath)!;
            Directory.CreateDirectory(directory);

            // Write to a temp file in the same directory and atomically replace the target
            var tempPath = Path.Combine(directory, $"{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
            string? backupPath = null;

            try
            {
                await File.WriteAllTextAsync(tempPath, yaml);

                // If destination exists, keep a short-lived backup to guard against rare replace failures
                if (File.Exists(fullPath))
                {
                    backupPath = Path.Combine(directory, $"{Path.GetFileName(fullPath)}.bak.{Guid.NewGuid():N}");
                    File.Replace(tempPath, fullPath, backupPath, ignoreMetadataErrors: true);
                }
                else
                {
                    // No destination yet; move atomically into place
                    File.Move(tempPath, fullPath);
                }

                if (backupPath != null && File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
            }
            catch
            {
                // Clean up temp/backup but never disturb the original file on failure
                TryDeleteFile(tempPath);
                TryDeleteFile(backupPath);
                throw;
            }

            // Stage and commit only the specific file written
            await Task.Run(() =>
            {
                using var repo = new LibGit2Sharp.Repository(_repositoryPath);
                Commands.Stage(repo, relativePath);

                var status = repo.RetrieveStatus();
                if (!status.IsDirty)
                    return;

                var signature = new Signature("BackupChrono", "backupchrono@system", DateTimeOffset.Now);
                repo.Commit(commitMessage, signature, signature);
            });
        }
        finally
        {
            _gitLock.Release();
        }
    }

    /// <summary>
    /// Writes an object to a YAML file (relative to repository root) without committing.
    /// This is intended for multi-step operations (e.g., rename) that batch multiple changes into a single commit.
    /// Caller is responsible for invoking CommitChanges afterward.
    /// </summary>
    public async Task WriteYamlFile<T>(string relativePath, T data)
    {
        var fullPath = Path.Combine(_repositoryPath, relativePath);
        var yaml = _yamlSerializer.Serialize(data);

        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);

        // Write to a temp file in the same directory and atomically replace the target
        var tempPath = Path.Combine(directory, $"{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        string? backupPath = null;

        try
        {
            await File.WriteAllTextAsync(tempPath, yaml);

            if (File.Exists(fullPath))
            {
                backupPath = Path.Combine(directory, $"{Path.GetFileName(fullPath)}.bak.{Guid.NewGuid():N}");
                File.Replace(tempPath, fullPath, backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, fullPath);
            }

            if (backupPath != null && File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }
        catch
        {
            TryDeleteFile(tempPath);
            TryDeleteFile(backupPath);
            throw;
        }
    }

    /// <summary>
    /// Reads an object from a YAML file (relative to repository root).
    /// </summary>
    public async Task<T?> ReadYamlFile<T>(string relativePath) where T : class
    {
        var fullPath = Path.Combine(_repositoryPath, relativePath);
        
        if (!File.Exists(fullPath))
            return null;

        var yaml = await File.ReadAllTextAsync(fullPath);
        return _yamlDeserializer.Deserialize<T>(yaml);
    }

    /// <summary>
    /// Commits all changes in the repository with the given message.
    /// </summary>
    public async Task CommitChanges(string message)
    {
        await _gitLock.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                using var repo = new LibGit2Sharp.Repository(_repositoryPath);
                
                // Stage all changes
                Commands.Stage(repo, "*");

                // Check if there are changes to commit
                var status = repo.RetrieveStatus();
                if (!status.IsDirty)
                    return;

                var signature = new Signature("BackupChrono", "backupchrono@system", DateTimeOffset.Now);
                repo.Commit(message, signature, signature);
            });
        }
        finally
        {
            _gitLock.Release();
        }
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

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Swallow cleanup errors; temp/backup files are best-effort
        }
    }
}
