using AvitoLike.Data;
using AvitoLike.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AvitoLike.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FavoritesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<FavoritesController> _logger;

        public FavoritesController(AppDbContext context, ILogger<FavoritesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Добавить в избранное
        [HttpPost]
        public async Task<IActionResult> AddToFavorites([FromBody] FavoriteRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                // Проверка существования объявления
                var ad = await _context.Ads
                    .Include(a => a.User)
                    .FirstOrDefaultAsync(a => a.Id == request.AdId);

                if (ad == null)
                {
                    return NotFound(new { error = "Объявление не найдено" });
                }

                // Проверка дублирования
                var exists = await _context.Favorites
                    .AnyAsync(f => f.UserId == userId && f.AdId == request.AdId);

                if (exists)
                {
                    return BadRequest(new { error = "Объявление уже в избранном" });
                }

                // Создаем Favorite
                var favorite = new Favorite
                {
                    UserId = userId,
                    AdId = request.AdId,
                    AddedAt = DateTime.UtcNow
                };

                _context.Favorites.Add(favorite);
                await _context.SaveChangesAsync();

                // Создаем уведомление для владельца объявления
                if (ad.UserId != userId) // Если объявление не своё
                {
                    var notification = new Notification
                    {
                        UserId = ad.UserId, // Владелец объявления
                        Title = "Новое избранное",
                        Message = $"Ваше объявление '{ad.Title}' добавлено в избранное",
                        Type = "favorite",
                        RelatedId = ad.Id,
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    };

                    _context.Notifications.Add(notification);
                    await _context.SaveChangesAsync(); // Сохраняем в БД
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении в избранное");
                return StatusCode(500, new { error = "Не удалось добавить в избранное" });
            }
        }

        // Удалить из избранного
        [HttpDelete("{adId}")]
        public async Task<IActionResult> RemoveFromFavorites(int adId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var favorite = await _context.Favorites
                    .Include(f => f.Ad)
                    .ThenInclude(a => a.User)
                    .FirstOrDefaultAsync(f => f.UserId == userId && f.AdId == adId);

                if (favorite == null)
                {
                    return NotFound(new { error = "Объявление не найдено в избранном" });
                }

                _context.Favorites.Remove(favorite);
                await _context.SaveChangesAsync();

                // Создаем уведомление для владельца объявления
                if (favorite.Ad.UserId != userId) // Не создаем уведомление, если пользователь удалил свое же объявление
                {
                    var notification = new Notification
                    {
                        UserId = favorite.Ad.UserId,
                        Title = "Удалено из избранного",
                        Message = $"Ваше объявление '{favorite.Ad.Title}' удалено из избранного",
                        Type = "favorite",
                        RelatedId = favorite.Ad.Id,
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    };

                    _context.Notifications.Add(notification);
                    await _context.SaveChangesAsync();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении из избранного");
                return StatusCode(500, new { error = "Не удалось удалить из избранного" });
            }
        }

        // Проверить, находится ли объявление в избранном
        [HttpGet("check")]
        public async Task<IActionResult> IsFavorite([FromQuery] int adId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var isFavorite = await _context.Favorites
                    .AnyAsync(f => f.UserId == userId && f.AdId == adId);

                return Ok(new { isFavorite });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке избранного");
                return StatusCode(500, new { error = "Ошибка проверки" });
            }
        }

        // Получить все избранные объявления пользователя
        [HttpGet]
        public async Task<IActionResult> GetFavorites()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var favorites = await _context.Favorites
                    .Where(f => f.UserId == userId)
                    .Include(f => f.Ad)
                    .ThenInclude(a => a.Images)
                    .Include(f => f.Ad)
                    .ThenInclude(a => a.Category)
                    .Select(f => new
                    {
                        f.Ad.Id,
                        f.Ad.Title,
                        f.Ad.Price,
                        f.Ad.Description,
                        f.Ad.Location,
                        Category = f.Ad.Category.Name,
                        ImageUrl = f.Ad.Images.FirstOrDefault(i => i.IsMain).Url ?? f.Ad.ImageUrl
                    })
                    .ToListAsync();

                return Ok(favorites);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении избранного");
                return StatusCode(500, new { error = "Не удалось получить избранное" });
            }
        }

        public class FavoriteRequest
        {
            public int AdId { get; set; }
        }
    }
}