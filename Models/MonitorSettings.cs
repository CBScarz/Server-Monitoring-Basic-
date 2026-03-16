namespace IMISMonitor.Models;

public class MonitorSettings
{
    public int PingIntervalSeconds { get; set; } = 3;
    public int PingTimeoutMs { get; set; } = 3000;
    public List<DeviceConfig> Devices { get; set; } = new();
}

public class DeviceConfig
{
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
}
