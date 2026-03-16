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

    public StatusHub(AppDbContext db, ILogger<StatusHub> logger)
    {
        _db = db;
    }

    public override async Task OnConnectedAsync()
    {
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
            // Log the error without using _logger
        }

        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {

        return base.OnDisconnectedAsync(exception);
    }
}
