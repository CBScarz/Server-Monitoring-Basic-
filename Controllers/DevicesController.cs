using IMISMonitor.Data;
using IMISMonitor.Models;
using IMISMonitor.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IMISMonitor.Controllers;

[ApiController]
[Route("api")]
public class DevicesController : ControllerBase
{
    private readonly AppDbContext _db;

    public DevicesController(AppDbContext db, ILogger<DevicesController> logger)
    {
        _db = db;
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

    [HttpGet("logs")]
    [ProducesResponseType(typeof(IEnumerable<DowntimeEvent>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int? deviceId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        try
        {
            var effectiveFrom = (from?.ToUniversalTime()) ?? DateTime.UtcNow.AddDays(-7);
            var effectiveTo = (to?.ToUniversalTime()) ?? DateTime.UtcNow;

            if (effectiveFrom > effectiveTo)
            {
                return BadRequest(new { error = "The 'from' date must be earlier than the 'to' date." });
            }

            var query = _db.DowntimeEvents
                .Where(e => e.WentOfflineAt >= effectiveFrom && e.WentOfflineAt <= effectiveTo)
                .AsQueryable();

            if (deviceId.HasValue)
            {
                query = query.Where(e => e.DeviceId == deviceId.Value);
            }

            var logs = await query
                .OrderByDescending(e => e.WentOfflineAt)
                .Take(500)
                .ToListAsync();

            return Ok(logs);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to retrieve logs." });
        }
    }

    [HttpGet("devices/{deviceId}/latency-trends")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetLatencyTrends([FromRoute] int deviceId, [FromQuery] int hours = 24)
    {
        try
        {
            var device = await _db.Devices.FindAsync(new object[] { deviceId }, cancellationToken: default);
            if (device == null)
            {
                return NotFound(new { error = "Device not found." });
            }
            return Ok(new[] { new { message = "Historical latency data not available. Only in-memory session statistics are tracked.", recordedAt = DateTime.UtcNow } });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to retrieve latency trends." });
        }
    }

    [HttpGet("devices/{deviceId}/stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDeviceStats([FromRoute] int deviceId)
    {
        try
        {
            var device = await _db.Devices
                .Where(d => d.Id == deviceId)
                .Select(d => new
                {
                    d.Id,
                    d.Name,
                    d.IpAddress,
                    d.Status,
                    d.LastChecked,
                    d.LastSeen,
                    d.LastLatencyMs,
                    d.AverageLatencyMs,
                    d.UptimePercentage
                })
                .FirstOrDefaultAsync();

            if (device == null)
            {
                return NotFound(new { error = "Device not found." });
            }

            return Ok(device);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to retrieve device stats." });
        }
    }
}
