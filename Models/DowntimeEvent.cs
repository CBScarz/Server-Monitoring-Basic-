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

    /// <summary>
    /// Computed duration of the downtime event. Returns the elapsed time since going offline
    /// if the device is still down, or the total downtime if it has recovered.
    /// </summary>
    [NotMapped]
    public TimeSpan? Duration => CameBackOnlineAt.HasValue
        ? CameBackOnlineAt.Value - WentOfflineAt
        : (DateTime?)null == null ? DateTime.UtcNow - WentOfflineAt : (TimeSpan?)null;
}
