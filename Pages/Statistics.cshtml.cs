using IMISMonitor.Models;
using IMISMonitor.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IMISMonitor.Pages;

public class StatisticsModel : PageModel
{
    private readonly ILogger<StatisticsModel> _logger;

    public IList<MonitoredDevice> Devices { get; set; } = new List<MonitoredDevice>();
    public Dictionary<int, DeviceStats> DeviceStatsMap { get; set; } = new Dictionary<int, DeviceStats>();

    public StatisticsModel(ILogger<StatisticsModel> logger)
    {
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

            // Calculate current statistics for each device
            await CalculateDeviceStatsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading statistics page data");
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
