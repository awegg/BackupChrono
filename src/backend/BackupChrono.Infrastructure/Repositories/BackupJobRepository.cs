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

    public BackupJobRepository(string repositoryPath, ILogger<BackupJobRepository> logger)
    {
        _jobsDirectory = Path.Combine(repositoryPath, "jobs");
        _logger = logger;
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
        var yaml = _yamlSerializer.Serialize(job);
        await File.WriteAllTextAsync(filePath, yaml);
        return job;
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

    private string GetJobFilePath(Guid jobId)
    {
        return Path.Combine(_jobsDirectory, $"{jobId}.yaml");
    }
}
