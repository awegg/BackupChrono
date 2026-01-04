using BackupChrono.Core.Entities;
using BackupChrono.Core.Interfaces;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace BackupChrono.Infrastructure.Repositories;

public class BackupJobRepository : IBackupJobRepository
{
    private readonly string _jobsDirectory;
    private readonly ISerializer _yamlSerializer;
    private readonly IDeserializer _yamlDeserializer;
    private readonly ILogger<BackupJobRepository> _logger;
    private readonly IDeviceService _deviceService;
    private readonly IShareService _shareService;

    public BackupJobRepository(string repositoryPath, ILogger<BackupJobRepository> logger, IDeviceService deviceService, IShareService shareService)
    {
        _jobsDirectory = Path.Combine(repositoryPath, "jobs");
        _logger = logger;
        _deviceService = deviceService;
        _shareService = shareService;
        Directory.CreateDirectory(_jobsDirectory);
        
        _yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
            
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
    }

    public async Task<BackupJob> SaveJob(BackupJob job)
    {
        var filePath = GetJobFilePath(job.Id);
        var directory = Path.GetDirectoryName(filePath) ?? _jobsDirectory;
        var tempPath = Path.Combine(directory, $"{job.Id}.tmp.{Guid.NewGuid():N}.yaml");
        
        try
        {
            // Serialize and write to temp file
            var yaml = _yamlSerializer.Serialize(job);
            await File.WriteAllTextAsync(tempPath, yaml);
            
            // Atomic replace
            if (File.Exists(filePath))
            {
                File.Replace(tempPath, filePath, null);
            }
            else
            {
                File.Move(tempPath, filePath, overwrite: true);
            }
            
            return job;
        }
        catch
        {
            // Clean up temp file on failure
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* Ignore cleanup errors */ }
            }
            throw;
        }
    }

    public async Task<BackupJob?> GetJob(Guid jobId)
    {
        var filePath = GetJobFilePath(jobId);
        if (!File.Exists(filePath))
        {
            return null;
        }

        var yaml = await File.ReadAllTextAsync(filePath);
        return _yamlDeserializer.Deserialize<BackupJob>(yaml);
    }

    public async Task<List<BackupJob>> ListJobs()
    {
        var jobs = new List<BackupJob>();
        
        if (!Directory.Exists(_jobsDirectory))
        {
            return jobs;
        }

        var files = Directory.GetFiles(_jobsDirectory, "*.yaml");
        foreach (var file in files)
        {
            try
            {
                var yaml = await File.ReadAllTextAsync(file);
                var job = _yamlDeserializer.Deserialize<BackupJob>(yaml);
                if (job != null)
                {
                    // Enrich job with device and share names if not already populated
                    await EnrichJobWithNames(job);
                    jobs.Add(job);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read job file {JobFile}", file);
            }
        }

        return jobs.OrderByDescending(j => j.StartedAt ?? DateTime.MinValue).ToList();
    }

    private async Task EnrichJobWithNames(BackupJob job)
    {
        try
        {
            // Only populate if not already set (from new jobs)
            if (string.IsNullOrEmpty(job.DeviceName))
            {
                var device = await _deviceService.GetDevice(job.DeviceId);
                if (device != null)
                {
                    job.DeviceName = device.Name;
                }
            }

            if (job.ShareId.HasValue && string.IsNullOrEmpty(job.ShareName))
            {
                var share = await _shareService.GetShare(job.ShareId.Value);
                if (share != null)
                {
                    job.ShareName = share.Name;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enrich job {JobId} with names", job.Id);
            // Don't throw - continue with whatever data we have
        }
    }

    public async Task<List<BackupJob>> ListJobsByDevice(Guid deviceId)
    {
        var allJobs = await ListJobs();
        return allJobs.Where(j => j.DeviceId == deviceId).ToList();
    }

    public async Task<List<BackupJob>> ListJobsByStatus(BackupJobStatus status)
    {
        var allJobs = await ListJobs();
        return allJobs.Where(j => j.Status == status).ToList();
    }

    public Task<bool> DeleteJob(Guid jobId)
    {
        var filePath = GetJobFilePath(jobId);
        if (!File.Exists(filePath))
        {
            return Task.FromResult(false);
        }

        try
        {
            File.Delete(filePath);
            _logger.LogInformation("Deleted backup job {JobId}", jobId);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete job file {JobFile}", filePath);
            throw;
        }
    }

    private string GetJobFilePath(Guid jobId)
    {
        return Path.Combine(_jobsDirectory, $"{jobId}.yaml");
    }
}
