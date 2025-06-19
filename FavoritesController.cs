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

        // �������� � ���������
        [HttpPost]
        public async Task<IActionResult> AddToFavorites([FromBody] FavoriteRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                // �������� ������������� ����������
                var ad = await _context.Ads
                    .Include(a => a.User)
                    .FirstOrDefaultAsync(a => a.Id == request.AdId);

                if (ad == null)
                {
                    return NotFound(new { error = "���������� �� �������" });
                }

                // �������� ������������
                var exists = await _context.Favorites
                    .AnyAsync(f => f.UserId == userId && f.AdId == request.AdId);

                if (exists)
                {
                    return BadRequest(new { error = "���������� ��� � ���������" });
                }

                // ������� Favorite
                var favorite = new Favorite
                {
                    UserId = userId,
                    AdId = request.AdId,
                    AddedAt = DateTime.UtcNow
                };

                _context.Favorites.Add(favorite);
                await _context.SaveChangesAsync();

                // ������� ����������� ��� ��������� ����������
                if (ad.UserId != userId) // ���� ���������� �� ���
                {
                    var notification = new Notification
                    {
                        UserId = ad.UserId, // �������� ����������
                        Title = "����� ���������",
                        Message = $"���� ���������� '{ad.Title}' ��������� � ���������",
                        Type = "favorite",
                        RelatedId = ad.Id,
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    };

                    _context.Notifications.Add(notification);
                    await _context.SaveChangesAsync(); // ��������� � ��
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "������ ��� ���������� � ���������");
                return StatusCode(500, new { error = "�� ������� �������� � ���������" });
            }
        }

        // ������� �� ����������
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
                    return NotFound(new { error = "���������� �� ������� � ���������" });
                }

                _context.Favorites.Remove(favorite);
                await _context.SaveChangesAsync();

                // ������� ����������� ��� ��������� ����������
                if (favorite.Ad.UserId != userId) // �� ������� �����������, ���� ������������ ������ ���� �� ����������
                {
                    var notification = new Notification
                    {
                        UserId = favorite.Ad.UserId,
                        Title = "������� �� ����������",
                        Message = $"���� ���������� '{favorite.Ad.Title}' ������� �� ����������",
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
                _logger.LogError(ex, "������ ��� �������� �� ����������");
                return StatusCode(500, new { error = "�� ������� ������� �� ����������" });
            }
        }

        // ���������, ��������� �� ���������� � ���������
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
                _logger.LogError(ex, "������ ��� �������� ����������");
                return StatusCode(500, new { error = "������ ��������" });
            }
        }

        // �������� ��� ��������� ���������� ������������
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
                _logger.LogError(ex, "������ ��� ��������� ����������");
                return StatusCode(500, new { error = "�� ������� �������� ���������" });
            }
        }

        public class FavoriteRequest
        {
            public int AdId { get; set; }
        }
    }
}