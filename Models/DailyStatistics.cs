namespace IMISMonitor.Models;

public class DailyStatistics
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;

    
    public DateTime PeriodStartUtc { get; set; }

  
    public DateTime PeriodEndUtc { get; set; }
   
    public int TotalPings { get; set; }

    public int SuccessfulPings { get; set; }
 
    public double UptimePercentage { get; set; }

    public double? AverageLatencyMs { get; set; }

    public DateTime ArchivedAtUtc { get; set; } = DateTime.UtcNow;
}
