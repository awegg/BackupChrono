using Microsoft.AspNetCore.SignalR;

namespace BackupChrono.Api.Hubs;

/// <summary>
/// SignalR hub for broadcasting real-time backup progress updates to connected clients.
/// </summary>
public class BackupProgressHub : Hub
{
    private readonly ILogger<BackupProgressHub> _logger;

    public BackupProgressHub(ILogger<BackupProgressHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
