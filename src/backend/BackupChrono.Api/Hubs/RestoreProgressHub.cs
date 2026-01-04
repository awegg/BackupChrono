using Microsoft.AspNetCore.SignalR;

namespace BackupChrono.Api.Hubs;

/// <summary>
/// SignalR hub for broadcasting real-time restore progress updates to connected clients.
/// </summary>
public class RestoreProgressHub : Hub
{
    private readonly ILogger<RestoreProgressHub> _logger;

    public RestoreProgressHub(ILogger<RestoreProgressHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected to restore hub: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected from restore hub: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
