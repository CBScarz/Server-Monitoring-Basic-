using IMISMonitor.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace IMISMonitor.Hubs;

/// <summary>
/// SignalR hub for real-time device status updates.
/// The hub itself is minimal — <see cref="Services.PingMonitorService"/> pushes
/// updates via <see cref="IHubContext{StatusHub}"/> after every ping sweep.
/// On connection, the client immediately receives a snapshot of the current state.
/// 
/// SECURITY: Add [Authorize] attribute once authentication is configured.
/// [Authorize]
/// </summary>
public class StatusHub : Hub
{
    private readonly AppDbContext _db;
    private readonly ILogger<StatusHub> _logger;

    public StatusHub(AppDbContext db, ILogger<StatusHub> logger)
    {
        _db = db;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("SignalR client connected: {ConnectionId}", Context.ConnectionId);

        try
        {
            var snapshot = await _db.Devices
                .OrderBy(d => d.Name)
                .Select(d => new
                {
                    d.Id,
                    d.Name,
                    d.IpAddress,
                    d.Status,
                    d.LastSeen,
                    d.LastChecked
                })
                .ToListAsync();

            // Send the current state snapshot to the newly connected client only.
            await Clients.Caller.SendAsync("StatusUpdate", snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send initial status snapshot to {ConnectionId}", Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception is null)
        {
            _logger.LogInformation("SignalR client disconnected: {ConnectionId}", Context.ConnectionId);
        }
        else
        {
            _logger.LogWarning(exception, "SignalR client disconnected with error: {ConnectionId}", Context.ConnectionId);
        }

        return base.OnDisconnectedAsync(exception);
    }
}
