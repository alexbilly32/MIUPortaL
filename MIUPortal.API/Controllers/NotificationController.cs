using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;
using MIUPortal.API.Models;

namespace MIUPortal.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationController : ControllerBase
    {
        private readonly MIUContext _context;
        private readonly ILogger<NotificationController> _logger;

        public NotificationController(MIUContext context, ILogger<NotificationController> logger)
        {
            _context = context;
            _logger = logger;
        }

        
        [HttpGet("{regNumber}")]
        public async Task<IActionResult> GetNotifications(string regNumber)
        {
            try
            {
                var notifications = await _context.Notifications
                    .Where(n => n.RegNumber == regNumber)
                    .OrderByDescending(n => n.DateSent)
                    .ToListAsync();

                var unreadCount = notifications.Count(n => !n.IsRead);

                return Ok(new
                {
                    success = true,
                    unreadCount = unreadCount,
                    data = notifications
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetNotifications");
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }

       
        [HttpPut("{notificationId}/read")]
        public async Task<IActionResult> MarkAsRead(int notificationId)
        {
            try
            {
                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.NotificationId == notificationId);

                if (notification == null)
                {
                    return NotFound(new { message = "Notification not found" });
                }

                notification.IsRead = true;
                notification.DateRead = DateTime.Now;

                _context.Notifications.Update(notification);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Notification marked as read" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MarkAsRead");
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendNotification([FromBody] SendNotificationRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.RegNumber))
                {
                    return BadRequest(new { message = "Invalid notification data" });
                }

                var notification = new Notification
                {
                    RegNumber = request.RegNumber,
                    Title = request.Title,
                    Message = request.Message,
                    Type = request.Type,
                    DateSent = DateTime.Now,
                    IsRead = false,
                    CreatedAt = DateTime.Now
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Notification sent", data = notification });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendNotification");
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }

        
        [HttpDelete("{notificationId}")]
        public async Task<IActionResult> DeleteNotification(int notificationId)
        {
            try
            {
                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.NotificationId == notificationId);

                if (notification == null)
                {
                    return NotFound(new { message = "Notification not found" });
                }

                _context.Notifications.Remove(notification);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Notification deleted" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeleteNotification");
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }
    }

    public class SendNotificationRequest
    {
        public string? RegNumber { get; set; }
        public string? Title { get; set; }
        public string? Message { get; set; }
        public string? Type { get; set; }
    }
}
