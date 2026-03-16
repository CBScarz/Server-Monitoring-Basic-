using System.ComponentModel.DataAnnotations.Schema;

namespace IMISMonitor.Models;

public class DowntimeEvent
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public DateTime WentOfflineAt { get; set; }
    public DateTime? CameBackOnlineAt { get; set; }

    [NotMapped]
    public TimeSpan? Duration => CameBackOnlineAt.HasValue
        ? CameBackOnlineAt.Value - WentOfflineAt
        : (DateTime?)null == null ? DateTime.UtcNow - WentOfflineAt : (TimeSpan?)null;
}
