using AvitoLike.Data;
using AvitoLike.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Linq;
using System.Threading.Tasks;

namespace AvitoLike.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(AppDbContext context, ILogger<NotificationsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Получить количество непрочитанных уведомлений
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var count = await _context.Notifications
                    .CountAsync(n => n.UserId == userId && !n.IsRead);

                return Ok(new { count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении количества непрочитанных уведомлений");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // Получить все уведомления пользователя
        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var notifications = await _context.Notifications
                    .Where(n => n.UserId == userId)
                    .OrderByDescending(n => n.CreatedAt)
                    .Select(n => new
                    {
                        n.Id,
                        n.Title,
                        n.Message,
                        n.Type,
                        n.IsRead,
                        n.RelatedId,
                        CreatedAt = n.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                        Icon = GetIconForType(n.Type)
                    })
                    .Take(50)
                    .ToListAsync();

                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении уведомлений");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // Пометка уведомления как прочитанного
        [HttpPost("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

                if (notification == null)
                    return NotFound(new { error = "Уведомление не найдено" });

                notification.IsRead = true;
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при пометке уведомления как прочитанного");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // Пометка всех уведомлений как прочитанных
        [HttpPost("mark-all-read")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                await _context.Notifications
                    .Where(n => n.UserId == userId && !n.IsRead)
                    .ExecuteUpdateAsync(n => n.SetProperty(x => x.IsRead, true));

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при пометке всех уведомлений как прочитанных");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // Создание нового уведомления
        [HttpPost]
        public async Task<IActionResult> CreateNotification([FromBody] CreateNotificationRequest request)
        {
            try
            {
                var notification = new Notification
                {
                    UserId = request.UserId,
                    Title = request.Title,
                    Message = request.Message,
                    Type = request.Type,
                    RelatedId = request.RelatedId,
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    notification.Id,
                    notification.Title,
                    notification.Message,
                    notification.Type,
                    notification.IsRead,
                    notification.RelatedId,
                    CreatedAt = notification.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                    Icon = GetIconForType(notification.Type)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании уведомления");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // Вспомогательный метод для получения иконки по типу уведомления
        private static string GetIconForType(string type)
        {
            return type switch
            {
                "message" => "✉️",      // Сообщения
                "favorite" => "❤️",     // Добавление в избранное
                "price_change" => "💰", // Изменение цены
                "new_review" => "⭐",   // Новый отзыв
                _ => "🔔"               // Остальные уведомления
            };
        }
    }
}