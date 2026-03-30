namespace IMISMonitor.Models;

public class MonitoredDevice
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string Status { get; set; } = "Unknown";
    public DateTime? LastSeen { get; set; }
    public DateTime LastChecked { get; set; }
    public int? LastLatencyMs { get; set; }
    public double? AverageLatencyMs { get; set; }
    public double? UptimePercentage { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int CurrentSessionTotalPings { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int CurrentSessionSuccessfulPings { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public DateTime CurrentSessionStartTime { get; set; } = DateTime.UtcNow;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public List<int> CurrentSessionLatencies { get; set; } = new List<int>();
}
