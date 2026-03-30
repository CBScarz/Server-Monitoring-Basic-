using IMISMonitor.Services;
using Microsoft.AspNetCore.SignalR;

namespace IMISMonitor.Hubs;

public class StatusHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        try
        {
            var snapshot = PingMonitorService.GetCachedDevices()
                .OrderBy(d => d.Name)
                .Select(d => new
                {
                    d.Id,
                    d.Name,
                    d.IpAddress,
                    d.MacAddress,
                    d.Status,
                    d.LastSeen,
                    d.LastChecked,
                    d.LastLatencyMs,
                    d.AverageLatencyMs,
                    d.UptimePercentage,
                    d.CurrentSessionTotalPings,
                    d.CurrentSessionSuccessfulPings
                })
                .ToList();

            await Clients.Caller.SendAsync("StatusUpdate", snapshot);
        }
        catch (Exception ex)
        {
        }

        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {

        return base.OnDisconnectedAsync(exception);
    }
}
