using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;
using MIUPortal.API.Models;

namespace MIUPortal.API.Services
{
    public interface INotificationService
    {
        Task<List<Notification>> GetStudentNotificationsAsync(string regNumber);
        Task<int> GetUnreadCountAsync(string regNumber);
        Task<bool> SendNotificationAsync(string regNumber, string title, string message, string type);
        Task<bool> MarkAsReadAsync(int notificationId);
    }

    public class NotificationService : INotificationService
    {
        private readonly MIUContext _context;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(MIUContext context, ILogger<NotificationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<Notification>> GetStudentNotificationsAsync(string regNumber)
        {
            try
            {
                return await _context.Notifications
                    .Where(n => n.RegNumber == regNumber)
                    .OrderByDescending(n => n.DateSent)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notifications");
                return new List<Notification>();
            }
        }

        public async Task<int> GetUnreadCountAsync(string regNumber)
        {
            try
            {
                return await _context.Notifications
                    .CountAsync(n => n.RegNumber == regNumber && !n.IsRead);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread count");
                return 0;
            }
        }

        public async Task<bool> SendNotificationAsync(string regNumber, string title, string message, string type)
        {
            try
            {
                var notification = new Notification
                {
                    RegNumber = regNumber,
                    Title = title,
                    Message = message,
                    Type = type,
                    DateSent = DateTime.Now,
                    IsRead = false,
                    CreatedAt = DateTime.Now
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification");
                return false;
            }
        }

        public async Task<bool> MarkAsReadAsync(int notificationId)
        {
            try
            {
                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.NotificationId == notificationId);

                if (notification == null)
                    return false;

                notification.IsRead = true;
                notification.DateRead = DateTime.Now;

                _context.Notifications.Update(notification);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification as read");
                return false;
            }
        }
    }
}
