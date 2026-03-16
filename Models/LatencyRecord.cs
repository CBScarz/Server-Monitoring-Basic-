namespace IMISMonitor.Models;


public class LatencyRecord
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public DateTime RecordedAt { get; set; }
    public int? LatencyMs { get; set; }
    public bool WasSuccessful { get; set; }
}
