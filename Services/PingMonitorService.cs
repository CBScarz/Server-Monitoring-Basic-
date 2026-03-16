using System.Net.NetworkInformation;
using IMISMonitor.Data;
using IMISMonitor.Hubs;
using IMISMonitor.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IMISMonitor.Services;

public class PingMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<StatusHub> _hubContext;
    private readonly MonitorSettings _settings;

    // Keep devices in-memory across ping cycles to preserve session counters
    private static readonly Dictionary<int, MonitoredDevice> _inMemoryDeviceCache = new();
    private static readonly object _cacheLock = new object();

    public PingMonitorService(
        IServiceScopeFactory scopeFactory,
        IHubContext<StatusHub> hubContext,
        IOptions<MonitorSettings> settings,
        ILogger<PingMonitorService> logger)
    {
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        await SeedDevicesAsync(stoppingToken);

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
    public static IList<MonitoredDevice> GetCachedDevices()
    {
        lock (_cacheLock)
        {
            return _inMemoryDeviceCache.Values.ToList();
        }
    }

    private async Task SeedDevicesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existingDevices = await db.Devices.ToListAsync(ct);
        var configByIp = _settings.Devices
            .GroupBy(d => d.IpAddress)
            .Select(g => g.First())
            .ToDictionary(d => d.IpAddress, StringComparer.OrdinalIgnoreCase);

        var staleDevices = existingDevices
            .Where(d => !configByIp.ContainsKey(d.IpAddress))
            .ToList();

        if (staleDevices.Count > 0)
        {
            db.Devices.RemoveRange(staleDevices);
        }

        foreach (var deviceConfig in configByIp.Values)
        {
            var existing = existingDevices.FirstOrDefault(d =>
                string.Equals(d.IpAddress, deviceConfig.IpAddress, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                db.Devices.Add(new MonitoredDevice
                {
                    Name = deviceConfig.Name,
                    IpAddress = deviceConfig.IpAddress,
                    Status = "Unknown",
                    LastChecked = DateTime.UtcNow
                });
                continue;
            }

            if (!string.Equals(existing.Name, deviceConfig.Name, StringComparison.Ordinal))
            {
                existing.Name = deviceConfig.Name;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task RunPingCycleAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        List<MonitoredDevice> devices;
        try
        {
            
            lock (_cacheLock)
            {
                if (_inMemoryDeviceCache.Count > 0)
                {
                    devices = _inMemoryDeviceCache.Values.ToList();
                }
                else
                {
                    
                    devices = db.Devices.ToList();
                    foreach (var device in devices)
                    {
                        _inMemoryDeviceCache[device.Id] = device;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            return;
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
                await UpdateDeviceStatusAsync(db, device, pingResult.newStatus, pingResult.latencyMs, ct);
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
                    d.Status,
                    d.LastSeen,
                    d.LastChecked,
                    d.LastLatencyMs,
                    d.AverageLatencyMs,
                    d.UptimePercentage
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
        AppDbContext db,
        MonitoredDevice device,
        string newStatus,
        int? latencyMs,
        CancellationToken ct)
    {
        string previousStatus = device.Status;
        bool wasOnline = previousStatus == "Online";
        bool isNowOnline = newStatus == "Online";
        bool wasOfflineOrTimeout = previousStatus is "Offline" or "Timeout";
        bool isNowOfflineOrTimeout = newStatus is "Offline" or "Timeout";

        device.CurrentSessionTotalPings++;
        if (isNowOnline && latencyMs.HasValue)
        {
            device.LastLatencyMs = latencyMs.Value;
            device.CurrentSessionSuccessfulPings++;
            device.CurrentSessionLatencies.Add(latencyMs.Value);
        }

        if (wasOnline && isNowOfflineOrTimeout)
        {
            db.DowntimeEvents.Add(new DowntimeEvent
            {
                DeviceId = device.Id,
                DeviceName = device.Name,
                IpAddress = device.IpAddress,
                WentOfflineAt = DateTime.UtcNow,
                CameBackOnlineAt = null
            });
        }

        
        if (wasOfflineOrTimeout && isNowOnline)
        {
            var openEvent = await db.DowntimeEvents
                .Where(e => e.DeviceId == device.Id && e.CameBackOnlineAt == null)
                .OrderByDescending(e => e.WentOfflineAt)
                .FirstOrDefaultAsync(ct);

            if (openEvent != null)
            {
                var downtimeDuration = DateTime.UtcNow - openEvent.WentOfflineAt;
                
                if (downtimeDuration.TotalMinutes >= 2)
                {
                    openEvent.CameBackOnlineAt = DateTime.UtcNow;
                }
                else
                {
                    db.DowntimeEvents.Remove(openEvent);
                }
            }
        }

        device.Status = newStatus;
        device.LastChecked = DateTime.UtcNow;

        if (isNowOnline)
        {
            device.LastSeen = DateTime.UtcNow;
        }

        if (device.CurrentSessionTotalPings > 0)
        {
            device.UptimePercentage = (device.CurrentSessionSuccessfulPings * 100.0) / device.CurrentSessionTotalPings;
        }

        if (device.CurrentSessionLatencies.Count > 0)
        {
            device.AverageLatencyMs = device.CurrentSessionLatencies.Average();
        }

        var sessionDuration = DateTime.UtcNow - device.CurrentSessionStartTime;
        if (sessionDuration.TotalHours >= 24)
        {
            await ArchiveDaily24hrStatsAndResetAsync(db, device, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task ArchiveDaily24hrStatsAndResetAsync(AppDbContext db, MonitoredDevice device, CancellationToken ct)
    {
        var dailyStats = new DailyStatistics
        {
            DeviceId = device.Id,
            DeviceName = device.Name,
            IpAddress = device.IpAddress,
            PeriodStartUtc = device.CurrentSessionStartTime,
            PeriodEndUtc = DateTime.UtcNow,
            TotalPings = device.CurrentSessionTotalPings,
            SuccessfulPings = device.CurrentSessionSuccessfulPings,
            UptimePercentage = device.UptimePercentage ?? 0,
            AverageLatencyMs = device.CurrentSessionLatencies.Count > 0 ? device.CurrentSessionLatencies.Average() : null,
            ArchivedAtUtc = DateTime.UtcNow
        };

        db.DailyStatistics.Add(dailyStats);

        device.CurrentSessionTotalPings = 0;
        device.CurrentSessionSuccessfulPings = 0;
        device.CurrentSessionLatencies.Clear();
        device.CurrentSessionStartTime = DateTime.UtcNow;
        device.AverageLatencyMs = null;
        device.UptimePercentage = null;

        await db.SaveChangesAsync(ct);
    }
}


