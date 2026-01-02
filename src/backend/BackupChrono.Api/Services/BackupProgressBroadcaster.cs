using BackupChrono.Api.Hubs;
using BackupChrono.Core.Interfaces;
using BackupChrono.Core.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace BackupChrono.Api.Services;

/// <summary>
/// Service that bridges BackupOrchestrator events to SignalR hub broadcasts.
/// </summary>
public class BackupProgressBroadcaster : IHostedService
{
    private readonly IBackupOrchestrator _orchestrator;
    private readonly IHubContext<BackupProgressHub> _hubContext;
    private readonly ILogger<BackupProgressBroadcaster> _logger;

    public BackupProgressBroadcaster(
        IBackupOrchestrator orchestrator,
        IHubContext<BackupProgressHub> hubContext,
        ILogger<BackupProgressBroadcaster> logger)
    {
        _orchestrator = orchestrator;
        _hubContext = hubContext;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _orchestrator.ProgressUpdated += OnProgressUpdated;
        _logger.LogInformation("BackupProgressBroadcaster started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _orchestrator.ProgressUpdated -= OnProgressUpdated;
        _logger.LogInformation("BackupProgressBroadcaster stopped");
        return Task.CompletedTask;
    }

    private async void OnProgressUpdated(object? sender, BackupProgress progress)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("BackupProgress", progress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast progress for job {JobId}", progress.JobId);
        }
    }
}
