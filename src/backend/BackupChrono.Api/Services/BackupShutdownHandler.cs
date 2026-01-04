using BackupChrono.Core.Interfaces;

namespace BackupChrono.Api.Services;

/// <summary>
/// Handles graceful shutdown of backup operations when the application stops.
/// </summary>
public class BackupShutdownHandler : IHostedService
{
    private readonly IBackupOrchestrator _orchestrator;
    private readonly ILogger<BackupShutdownHandler> _logger;

    public BackupShutdownHandler(
        IBackupOrchestrator orchestrator,
        ILogger<BackupShutdownHandler> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("BackupShutdownHandler started");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogWarning("Application shutting down - cancelling all backup jobs");
        try
        {
            var activeCount = _orchestrator.GetActiveJobCount();
            if (activeCount > 0)
            {
                _logger.LogWarning("Found {Count} active jobs - cancelling them now", activeCount);
                await _orchestrator.CancelAllJobs();
                
                // Give processes a moment to terminate
                await Task.Delay(1000, cancellationToken);
                
                _logger.LogInformation("All backup jobs cancelled successfully");
            }
            else
            {
                _logger.LogInformation("No active jobs to cancel");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling backup jobs during shutdown");
        }
    }
}
