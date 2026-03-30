using System.Net.NetworkInformation;
using IMISMonitor.Hubs;
using IMISMonitor.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace IMISMonitor.Services;

public class PingMonitorService : BackgroundService
{
    private readonly IHubContext<StatusHub> _hubContext;
    private readonly MonitorSettings _settings;

    // Keep devices in-memory across ping cycles to preserve session counters
    private static readonly Dictionary<int, MonitoredDevice> _inMemoryDeviceCache = new();
    private static readonly object _cacheLock = new object();

    public PingMonitorService(
        IHubContext<StatusHub> hubContext,
        IOptions<MonitorSettings> settings,
        ILogger<PingMonitorService> logger)
    {
        _hubContext = hubContext;
        _settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initialize devices from configuration
        InitializeDevicesFromConfig();

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunPingCycleAsync(stoppingToken);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_settings.PingIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void InitializeDevicesFromConfig()
    {
        lock (_cacheLock)
        {
            if (_inMemoryDeviceCache.Count > 0)
                return;

            int deviceId = 1;
            foreach (var deviceConfig in _settings.Devices)
            {
                var device = new MonitoredDevice
                {
                    Id = deviceId++,
                    Name = deviceConfig.Name,
                    IpAddress = deviceConfig.IpAddress,
                    MacAddress = deviceConfig.MacAddress,
                    Status = "Unknown",
                    LastChecked = DateTime.UtcNow
                };
                _inMemoryDeviceCache[device.Id] = device;
            }
        }
    }
    public static IList<MonitoredDevice> GetCachedDevices()
    {
        lock (_cacheLock)
        {
            return _inMemoryDeviceCache.Values.ToList();
        }
    }

    private async Task RunPingCycleAsync(CancellationToken ct)
    {
        List<MonitoredDevice> devices;
        lock (_cacheLock)
        {
            devices = _inMemoryDeviceCache.Values.ToList();
        }

        if (devices.Count == 0)
        {
            return;
        }

        foreach (var device in devices)
        {
            if (ct.IsCancellationRequested)
                break;

            (string newStatus, int? latencyMs) pingResult;
            try
            {
                pingResult = await PingDeviceAsync(device.IpAddress);
            }
            catch (Exception ex)
            {
                pingResult = ("Offline", null);
            }

            try
            {
                await UpdateDeviceStatusAsync(device, pingResult.newStatus, pingResult.latencyMs, ct);
            }
            catch (Exception ex)
            {
            }
        }

        try
        {
            var refreshed = devices
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

            await _hubContext.Clients.All.SendAsync("StatusUpdate", refreshed, ct);
        }
        catch (Exception ex)
        {
        }
    }

    private async Task<(string status, int? latencyMs)> PingDeviceAsync(string ipAddress)
    {
        using var ping = new Ping();
        try
        {
            PingReply reply = await ping.SendPingAsync(ipAddress, _settings.PingTimeoutMs);
            var status = reply.Status switch
            {
                IPStatus.Success => "Online",
                IPStatus.TimedOut => "Timeout",
                _ => "Offline"
            };

            int? latencyMs = reply.Status == IPStatus.Success ? (int)reply.RoundtripTime : null;
            return (status, latencyMs);
        }
        catch (PingException)
        {
            return ("Offline", null);
        }
    }

    private async Task UpdateDeviceStatusAsync(
        MonitoredDevice device,
        string newStatus,
        int? latencyMs,
        CancellationToken ct)
    {
        // Track ping counts
        device.CurrentSessionTotalPings++;
        if (newStatus == "Online" && latencyMs.HasValue)
        {
            device.LastLatencyMs = latencyMs.Value;
            device.CurrentSessionSuccessfulPings++;
            device.CurrentSessionLatencies.Add(latencyMs.Value);
        }

        // Update status and timestamp
        device.Status = newStatus;
        device.LastChecked = DateTime.UtcNow;

        if (newStatus == "Online")
        {
            device.LastSeen = DateTime.UtcNow;
        }

        // Calculate metrics from session data
        if (device.CurrentSessionTotalPings > 0)
        {
            device.UptimePercentage = (device.CurrentSessionSuccessfulPings * 100.0) / device.CurrentSessionTotalPings;
        }

        if (device.CurrentSessionLatencies.Count > 0)
        {
            device.AverageLatencyMs = device.CurrentSessionLatencies.Average();
        }

        // No database saves - status tracking only in-memory for dashboard
        await Task.CompletedTask;
    }
}


