namespace IMISMonitor.Models;

/// <summary>
/// Stores aggregated daily statistics for a device over a 24-hour period.
/// Created automatically when a 24-hour session completes.
/// </summary>
public class DailyStatistics
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>UTC start time of the 24-hour tracking period.</summary>
    public DateTime PeriodStartUtc { get; set; }

    /// <summary>UTC end time of the 24-hour tracking period.</summary>
    public DateTime PeriodEndUtc { get; set; }

    /// <summary>Total ping attempts during the 24-hour period.</summary>
    public int TotalPings { get; set; }

    /// <summary>Successful ping attempts during the 24-hour period.</summary>
    public int SuccessfulPings { get; set; }

    /// <summary>Uptime percentage (0-100) during the 24-hour period.</summary>
    public double UptimePercentage { get; set; }

    /// <summary>Average latency in milliseconds during the 24-hour period.</summary>
    public double? AverageLatencyMs { get; set; }

    /// <summary>When this daily statistic was archived to the database.</summary>
    public DateTime ArchivedAtUtc { get; set; } = DateTime.UtcNow;
}
