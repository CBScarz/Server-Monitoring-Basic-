using IMISMonitor.Models;
using IMISMonitor.Services;
using Microsoft.AspNetCore.Mvc;

namespace IMISMonitor.Controllers;

[ApiController]
[Route("api")]
public class DevicesController : ControllerBase
{
    public DevicesController(ILogger<DevicesController> logger)
    {
    }

    [HttpGet("devices")]
    [ProducesResponseType(typeof(IEnumerable<MonitoredDevice>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDevices()
    {
        try
        {
            var devices = PingMonitorService.GetCachedDevices()
                .OrderBy(d => d.Name)
                .ToList();

            foreach (var device in devices)
            {
                await CalculateDeviceMetricsAsync(device);
            }

            return Ok(devices);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to retrieve devices." });
        }
    }

    private async Task CalculateDeviceMetricsAsync(MonitoredDevice device)
    {
        await Task.CompletedTask;
    }

}
