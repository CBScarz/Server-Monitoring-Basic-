using IMISMonitor.Data;
using IMISMonitor.Models;
using IMISMonitor.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace IMISMonitor.Pages;

public class LogsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ILogger<LogsModel> _logger;

    public IList<DowntimeEvent> DowntimeEvents { get; set; } = new List<DowntimeEvent>();
    public IList<MonitoredDevice> Devices { get; set; } = new List<MonitoredDevice>();
    public Dictionary<int, DeviceStats> DeviceStatsMap { get; set; } = new Dictionary<int, DeviceStats>();

    public LogsModel(AppDbContext db, ILogger<LogsModel> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        try
        {
            // Load devices from in-memory cache (preserves session ping counters)
            Devices = PingMonitorService.GetCachedDevices()
                .OrderBy(d => d.Name)
                .ToList();

            // Load recent downtime events (last 7 days)
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
            DowntimeEvents = await _db.DowntimeEvents
                .Where(e => e.WentOfflineAt >= sevenDaysAgo)
                .OrderByDescending(e => e.WentOfflineAt)
                .Take(100)
                .ToListAsync();

            // Calculate current statistics for each device
            await CalculateDeviceStatsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading logs page data");
        }
    }

    private async Task CalculateDeviceStatsAsync()
    {
        foreach (var device in Devices)
        {
            // Use in-memory session stats tracked during ping cycles
            var stats = new DeviceStats
            {
                DeviceId = device.Id,
                TotalTests = device.CurrentSessionTotalPings,
                SuccessfulTests = device.CurrentSessionSuccessfulPings,
                FailedTests = device.CurrentSessionTotalPings - device.CurrentSessionSuccessfulPings,
                CurrentStatus = device.Status,
                LastLatency = device.LastLatencyMs,
                AverageLatency = device.AverageLatencyMs,
                UptimePercentage = device.UptimePercentage ?? 0,
                SessionStartTime = device.CurrentSessionStartTime
            };

            DeviceStatsMap[device.Id] = stats;
        }
        
        await Task.CompletedTask;
    }

    /// <summary>
    /// Statistics for a single device during the current application session.
    /// </summary>
    public class DeviceStats
    {
        public int DeviceId { get; set; }
        public int TotalTests { get; set; }
        public int SuccessfulTests { get; set; }
        public int FailedTests { get; set; }
        public string CurrentStatus { get; set; } = "Unknown";
        public int? LastLatency { get; set; }
        public double? AverageLatency { get; set; }
        public double UptimePercentage { get; set; }
        public DateTime SessionStartTime { get; set; }
    }
}
