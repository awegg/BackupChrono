using BackupChrono.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Quartz;

namespace BackupChrono.Infrastructure.Scheduling;

/// <summary>
/// Quartz job to apply retention policies on a scheduled basis (e.g., daily at 3 AM).
/// </summary>
[DisallowConcurrentExecution]
public class RetentionPolicyJob : IJob
{
    private readonly IRetentionPolicyService _retentionPolicyService;
    private readonly ILogger<RetentionPolicyJob> _logger;

    public RetentionPolicyJob(
        IRetentionPolicyService retentionPolicyService,
        ILogger<RetentionPolicyJob> logger)
    {
        _retentionPolicyService = retentionPolicyService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var jobId = context.FireInstanceId;
        _logger.LogInformation("Starting scheduled retention policy job: {JobId}", jobId);

        try
        {
            var results = await _retentionPolicyService.ApplyForAll(dryRun: false, cancellationToken: context.CancellationToken);
            
            var totalPruned = results.Sum(r => r.SnapshotsPruned);
            var totalReclaimed = results.Sum(r => r.SpaceReclaimedBytes);
            var errorCount = results.Count(r => !string.IsNullOrEmpty(r.Error));

            _logger.LogInformation(
                "Retention policy job completed: {JobId}, {Devices} devices processed, {Pruned} snapshots pruned, {Reclaimed} bytes reclaimed, {Errors} errors",
                jobId,
                results.Select(r => r.DeviceName).Distinct().Count(),
                totalPruned,
                totalReclaimed,
                errorCount);

            if (errorCount > 0)
            {
                _logger.LogWarning("Retention policy job completed with {ErrorCount} errors", errorCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Retention policy job failed: {JobId}", jobId);
            throw new JobExecutionException(ex, refireImmediately: false);
        }
    }
}
