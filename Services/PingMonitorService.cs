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
    private readonly ILogger<PingMonitorService> _logger;

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
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PingMonitorService starting. Interval: {Interval}s, Timeout: {Timeout}ms",
            _settings.PingIntervalSeconds, _settings.PingTimeoutMs);

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
                // Graceful shutdown — expected when the host is stopping.
                break;
            }
        }

        _logger.LogInformation("PingMonitorService stopped.");
    }

    /// <summary>
    /// Gets the in-memory cached devices with their current session statistics.
    /// This preserves the TotalPings and SuccessfulPings counters across requests.
    /// </summary>
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

        // Remove stale devices that are no longer in appsettings.json.
        var staleDevices = existingDevices
            .Where(d => !configByIp.ContainsKey(d.IpAddress))
            .ToList();

        if (staleDevices.Count > 0)
        {
            db.Devices.RemoveRange(staleDevices);
            _logger.LogInformation("Removed {Count} stale device(s) not found in MonitorSettings.Devices", staleDevices.Count);
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
                _logger.LogInformation("Seeded device {Name} ({IpAddress})", deviceConfig.Name, deviceConfig.IpAddress);
                continue;
            }

            // Keep name synced when the same IP is renamed in config.
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
            // Use in-memory cache if available, otherwise load from database
            lock (_cacheLock)
            {
                if (_inMemoryDeviceCache.Count > 0)
                {
                    devices = _inMemoryDeviceCache.Values.ToList();
                }
                else
                {
                    // First run: load from database synchronously to populate cache
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
            _logger.LogError(ex, "Failed to load devices — skipping this cycle");
            return;
        }

        if (devices.Count == 0)
        {
            _logger.LogWarning("No devices found in database. Check MonitorSettings.Devices in appsettings.json.");
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
                _logger.LogWarning(ex, "Unexpected error pinging {Name} ({IpAddress}) — marking Offline",
                    device.Name, device.IpAddress);
                pingResult = ("Offline", null);
            }

            try
            {
                await UpdateDeviceStatusAsync(db, device, pingResult.newStatus, pingResult.latencyMs, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist status update for {Name} ({IpAddress})",
                    device.Name, device.IpAddress);
            }
        }

        // Broadcast the updated devices to all SignalR clients
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
            _logger.LogInformation("Ping cycle complete — broadcast {Count} device statuses to all clients.",
                refreshed.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast StatusUpdate via SignalR");
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

            // Capture latency only on successful pings
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

        // Track ping attempt in-memory (not saved to database)
        device.CurrentSessionTotalPings++;
        if (isNowOnline && latencyMs.HasValue)
        {
            device.LastLatencyMs = latencyMs.Value;
            device.CurrentSessionSuccessfulPings++;
            // Track latency for average calculation
            device.CurrentSessionLatencies.Add(latencyMs.Value);
        }

        // Transition: Online → Offline/Timeout — open a new downtime event.
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
            _logger.LogWarning("ALERT: {Name} ({IpAddress}) transitioned Online → {Status}",
                device.Name, device.IpAddress, newStatus);
        }

        // Transition: Offline/Timeout → Online — close the most recent open downtime event if it exceeds 2 minutes.
        if (wasOfflineOrTimeout && isNowOnline)
        {
            var openEvent = await db.DowntimeEvents
                .Where(e => e.DeviceId == device.Id && e.CameBackOnlineAt == null)
                .OrderByDescending(e => e.WentOfflineAt)
                .FirstOrDefaultAsync(ct);

            if (openEvent != null)
            {
                var downtimeDuration = DateTime.UtcNow - openEvent.WentOfflineAt;
                
                // Only persist downtime events longer than 2 minutes to prevent database flooding
                if (downtimeDuration.TotalMinutes >= 2)
                {
                    openEvent.CameBackOnlineAt = DateTime.UtcNow;
                    _logger.LogInformation(
                        "RECOVERY: {Name} ({IpAddress}) is back Online after {Duration:F1} minutes.",
                        device.Name, device.IpAddress, downtimeDuration.TotalMinutes);
                }
                else
                {
                    // Discard downtime events shorter than 2 minutes
                    db.DowntimeEvents.Remove(openEvent);
                    _logger.LogDebug(
                        "TRANSIENT BLIP: {Name} ({IpAddress}) was down for {Duration:F2} seconds (below 2-minute threshold).",
                        device.Name, device.IpAddress, downtimeDuration.TotalSeconds);
                }
            }
        }

        device.Status = newStatus;
        device.LastChecked = DateTime.UtcNow;

        if (isNowOnline)
        {
            device.LastSeen = DateTime.UtcNow;
        }

        // Update device metrics from in-memory session stats
        if (device.CurrentSessionTotalPings > 0)
        {
            device.UptimePercentage = (device.CurrentSessionSuccessfulPings * 100.0) / device.CurrentSessionTotalPings;
        }

        // Calculate average latency from tracked values
        if (device.CurrentSessionLatencies.Count > 0)
        {
            device.AverageLatencyMs = device.CurrentSessionLatencies.Average();
        }

        // Check if 24 hours have passed — archive and reset session
        var sessionDuration = DateTime.UtcNow - device.CurrentSessionStartTime;
        if (sessionDuration.TotalHours >= 24)
        {
            await ArchiveDaily24hrStatsAndResetAsync(db, device, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task ArchiveDaily24hrStatsAndResetAsync(AppDbContext db, MonitoredDevice device, CancellationToken ct)
    {
        // Archive the current 24-hour stats
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
        _logger.LogInformation(
            "ARCHIVED: {Name} 24hr stats - {Total} pings, {Successful} successful, {Uptime:F1}% uptime, {AvgLatency:F0}ms avg latency",
            device.Name, device.CurrentSessionTotalPings, device.CurrentSessionSuccessfulPings,
            device.UptimePercentage ?? 0, device.AverageLatencyMs ?? 0);

        // Reset session counters
        device.CurrentSessionTotalPings = 0;
        device.CurrentSessionSuccessfulPings = 0;
        device.CurrentSessionLatencies.Clear();
        device.CurrentSessionStartTime = DateTime.UtcNow;
        device.AverageLatencyMs = null;
        device.UptimePercentage = null;

        await db.SaveChangesAsync(ct);
    }
}


