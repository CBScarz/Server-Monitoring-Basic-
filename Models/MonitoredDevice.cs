namespace IMISMonitor.Models;

public class MonitoredDevice
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>Current status: "Online", "Offline", "Timeout", or "Unknown".</summary>
    public string Status { get; set; } = "Unknown";

    /// <summary>UTC timestamp of the last successful ping.</summary>
    public DateTime? LastSeen { get; set; }

    /// <summary>UTC timestamp of the most recent ping attempt (successful or not).</summary>
    public DateTime LastChecked { get; set; }

    /// <summary>Latency of the last successful ping in milliseconds. Null if no successful ping yet.</summary>
    public int? LastLatencyMs { get; set; }

    /// <summary>Average latency in milliseconds over the last 24 hours. Null if no data yet.</summary>
    public double? AverageLatencyMs { get; set; }

    /// <summary>Uptime percentage over the last 24 hours (0-100). Null if insufficient data.</summary>
    public double? UptimePercentage { get; set; }

    /// <summary>Total ping attempts tracked for current application session (in-memory, not persisted).</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int CurrentSessionTotalPings { get; set; }

    /// <summary>Successful ping attempts tracked for current application session (in-memory, not persisted).</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int CurrentSessionSuccessfulPings { get; set; }

    /// <summary>Last session reset time for calculating daily stats (in-memory, not persisted).</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public DateTime CurrentSessionStartTime { get; set; } = DateTime.UtcNow;

    /// <summary>List of latency values tracked during current session for average calculation (in-memory, not persisted).</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public List<int> CurrentSessionLatencies { get; set; } = new List<int>();
}
