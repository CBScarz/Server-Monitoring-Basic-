namespace IMISMonitor.Models;

/// <summary>
/// Historical record of a device's response latency at a specific point in time.
/// Used for tracking latency trends and calculating average response times.
/// </summary>
public class LatencyRecord
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public DateTime RecordedAt { get; set; }
    
    /// <summary>Round-trip latency in milliseconds. Null if ping failed.</summary>
    public int? LatencyMs { get; set; }
    
    /// <summary>Whether the ping was successful.</summary>
    public bool WasSuccessful { get; set; }
}
