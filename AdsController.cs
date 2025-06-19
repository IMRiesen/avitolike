using AvitoLike.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using AvitoLike.Controllers;
using AvitoLike.Models;

namespace AvitoLike.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AdsController> _logger;

        public AdsController(AppDbContext context, ILogger<AdsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Получить все объявления с пагинацией и фильтрацией
        [HttpGet]
        public async Task<IActionResult> GetAllAds(
        [FromQuery] string category = "",
        [FromQuery] string search = "",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] bool includeFavoriteStatus = false)
        {
            try
            {
                var userId = includeFavoriteStatus && User.Identity.IsAuthenticated
                    ? int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value)
                    : -1;

                var query = _context.Ads
                    .AsNoTracking()
                    .Include(a => a.User)
                    .Include(a => a.Category)
                    .Include(a => a.Images)
                    .Where(a => a.Status == "active");

                // Фильтрация по категории
                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(a => a.Category.Name == category);
                }

                // Фильтрация по поисковому запросу
                if (!string.IsNullOrEmpty(search))
                {
                    var searchLower = search.ToLower();
                    query = query.Where(a =>
                        a.Title.ToLower().Contains(searchLower) ||
                        a.Description.ToLower().Contains(searchLower) ||
                        a.Location.ToLower().Contains(searchLower));
                }

                var totalAds = await query.CountAsync();
                var ads = await query
                    .OrderByDescending(a => a.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Загружаем избранные ID одним запросом
                var favoriteAdIds = includeFavoriteStatus && userId > 0
                    ? await _context.Favorites
                        .Where(f => f.UserId == userId)
                        .Select(f => f.AdId)
                        .ToListAsync()
                    : new List<int>();

                var result = ads.Select(ad => new
                {
                    id = ad.Id,
                    title = ad.Title,
                    description = ad.Description,
                    price = ad.Price,
                    category = ad.Category?.Name ?? "Без категории",
                    location = ad.Location,
                    imageUrl = ad.Images.FirstOrDefault(i => i.IsMain)?.Url ?? ad.ImageUrl,
                    sellerName = ad.User?.Username ?? "Неизвестный",
                    phone = ad.User?.Phone ?? "",
                    sellerSince = ad.User?.CreatedAt.ToString("o"),
                    date = ad.CreatedAt.ToString("o"),
                    views = ad.ViewsCount,
                    userId = ad.UserId,
                    isFavorite = favoriteAdIds.Contains(ad.Id) // Проверяем по списку ID
                }).ToList();

                _logger.LogInformation("Получено {count} объявлений", ads.Count);

                return Ok(new
                {
                    data = result,
                    totalCount = totalAds,
                    page,
                    pageSize
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении объявлений");
                return StatusCode(500, new { error = "Не удалось получить объявления" });
            }
        }


        // Получить объявление по ID
        [HttpGet("{id}")]
        public async Task<IActionResult> GetAdById(int id)
        {
            try
            {
                var ad = await _context.Ads
                    .Include(a => a.User)
                    .Include(a => a.Category)
                    .Include(a => a.Images)
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (ad == null)
                {
                    return NotFound(new { error = $"Объявление с ID {id} не найдено" });
                }

                // Увеличиваем счетчик просмотров
                ad.ViewsCount++;
                await _context.SaveChangesAsync();

                var result = new
                {
                    id = ad.Id,
                    title = ad.Title,
                    description = ad.Description,
                    price = ad.Price,
                    category = ad.Category?.Name ?? "Без категории",
                    location = ad.Location,
                    imageUrl = ad.Images.FirstOrDefault(i => i.IsMain)?.Url ?? ad.ImageUrl,
                    sellerName = ad.User?.Username ?? "Неизвестный",
                    phone = ad.User?.Phone ?? "",
                    sellerSince = ad.User?.CreatedAt.ToString("o"),
                    date = ad.CreatedAt.ToString("o"),
                    views = ad.ViewsCount,
                    userId = ad.UserId
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении объявления ID {id}", id);
                return StatusCode(500, new { error = "Не удалось получить объявление" });
            }
        }

        [HttpGet("{id}/reviews")]
        public async Task<IActionResult> GetAdReviews(int id)
        {
            try
            {
                var reviews = await _context.Reviews
                    .Where(r => r.AdId == id)
                    .Include(r => r.Author)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();

                var result = reviews.Select(r => new
                {
                    id = r.Id,
                    authorId = r.AuthorId,
                    authorName = r.Author.Username,
                    rating = r.Rating,
                    comment = r.Comment,
                    date = r.CreatedAt.ToString("o")
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении отзывов");
                return StatusCode(500, new { error = "Не удалось получить отзывы" });
            }
        }

        [HttpPost("{id}/reviews")]
        [Authorize]
        public async Task<IActionResult> AddReview(int id, [FromBody] ReviewRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var ad = await _context.Ads.FindAsync(id);

                if (ad == null)
                {
                    return NotFound(new { error = "Объявление не найдено" });
                }

                // Проверяем, не оставлял ли уже пользователь отзыв
                var existingReview = await _context.Reviews
                    .FirstOrDefaultAsync(r => r.AdId == id && r.AuthorId == userId);

                if (existingReview != null)
                {
                    return BadRequest(new { error = "Вы уже оставляли отзыв на это объявление" });
                }

                var review = new Review
                {
                    AuthorId = userId,
                    TargetUserId = ad.UserId,
                    AdId = id,
                    Rating = request.Rating,
                    Comment = request.Comment,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Reviews.Add(review);
                await _context.SaveChangesAsync();

                // Получаем имя автора
                var author = await _context.Users.FindAsync(userId);

                return Ok(new
                {
                    id = review.Id,
                    authorId = review.AuthorId,
                    authorName = author.Username,
                    rating = review.Rating,
                    comment = review.Comment,
                    date = review.CreatedAt.ToString("o")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении отзыва");
                return StatusCode(500, new { error = "Не удалось добавить отзыв", details = ex.Message });
            }
        }


        // Создать новое объявление
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateAd([FromBody] AdCreateRequest request)
        {
            try
            {
                // Получаем ID текущего пользователя
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var user = await _context.Users.FindAsync(userId);

                if (user == null)
                {
                    return Unauthorized(new { error = "Пользователь не найден" });
                }

                // Создаем объект объявления
                var ad = new Ad
                {
                    Title = request.Title,
                    Description = request.Description,
                    Price = request.Price,
                    CategoryId = request.CategoryId,
                    Location = request.Location,
                    ImageUrl = request.ImageUrl,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    ViewsCount = 0,
                    Status = "active"
                };

                _context.Ads.Add(ad);
                await _context.SaveChangesAsync();

                // Создаем основное изображение
                var mainImage = new AdImage
                {
                    AdId = ad.Id,
                    Url = ad.ImageUrl,
                    IsMain = true,
                    UploadDate = DateTime.UtcNow
                };
                _context.AdImages.Add(mainImage);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Создано новое объявление ID {id}", ad.Id);

                return CreatedAtAction(nameof(GetAdById), new { id = ad.Id }, new
                {
                    id = ad.Id,
                    title = ad.Title,
                    price = ad.Price,
                    imageUrl = ad.ImageUrl,
                    sellerName = user.Username,
                    phone = user.Phone
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании объявления");
                return StatusCode(500, new { error = "Не удалось создать объявление" });
            }
        }

        // Получить похожие объявления
        [HttpGet("relevant")]
        public async Task<IActionResult> GetRelevantAds([FromQuery] int adId)
        {
            try
            {
                var currentAd = await _context.Ads
                    .Include(a => a.Category)
                    .FirstOrDefaultAsync(a => a.Id == adId);

                if (currentAd == null)
                    return NotFound();

                var relevantAds = await _context.Ads
                    .Where(a => a.Id != adId && a.CategoryId == currentAd.CategoryId)
                    .Include(a => a.Images)
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(4)
                    .ToListAsync();

                var result = relevantAds.Select(ad => new
                {
                    id = ad.Id,
                    title = ad.Title,
                    price = ad.Price,
                    imageUrl = ad.Images.FirstOrDefault(i => i.IsMain)?.Url ?? ad.ImageUrl
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении похожих объявлений");
                return StatusCode(500);
            }
        }

        // Обновить объявление
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateAd(int id, [FromBody] AdUpdateRequest request)
        {
            try
            {
                var existingAd = await _context.Ads
                    .Include(a => a.Favorites)
                    .ThenInclude(f => f.User)
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (existingAd == null)
                {
                    return NotFound(new { error = $"Объявление с ID {id} не найдено" });
                }

                // Проверяем, изменилась ли цена
                bool priceChanged = existingAd.Price != request.Price;

                // Обновляем объявление
                existingAd.Title = request.Title;
                existingAd.Description = request.Description;
                existingAd.Price = request.Price;
                existingAd.CategoryId = request.CategoryId;
                existingAd.Location = request.Location;
                existingAd.ImageUrl = request.ImageUrl;
                existingAd.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Если цена изменилась, создаем уведомления для всех, кто добавил в избранное
                if (priceChanged)
                {
                    foreach (var favorite in existingAd.Favorites)
                    {
                        var notificationRequest = new CreateNotificationRequest
                        {
                            UserId = favorite.UserId,
                            Title = "Изменение цены",
                            Message = $"Цена на объявление '{existingAd.Title}' из вашего избранного изменилась",
                            Type = "price_change",
                            RelatedId = existingAd.Id
                        };

                        await new NotificationsController(_context, _logger as ILogger<NotificationsController>)
                              .CreateNotification(notificationRequest);

                    }
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении объявления ID {id}", id);
                return StatusCode(500, new { error = "Не удалось обновить объявление" });
            }
        }

        // Удалить объявление
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteAd(int id)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var ad = await _context.Ads
                    .Include(a => a.Images)
                    .Include(a => a.Favorites)
                    .Include(a => a.Reviews)
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (ad == null) return NotFound();

                // Проверяем, что пользователь удаляет СВОЕ объявление
                if (ad.UserId != userId)
                {
                    return Forbid();
                }

                // Удаляем связанные данные
                _context.AdImages.RemoveRange(ad.Images);
                _context.Favorites.RemoveRange(ad.Favorites);
                _context.Reviews.RemoveRange(ad.Reviews);

                // Удаляем само объявление
                _context.Ads.Remove(ad);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при удалении объявления ID {id}");
                return StatusCode(500, new { error = "Не удалось удалить объявление" });
            }
        }

        // Получить объявления пользователя
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserAds(int userId)
        {
            try
            {
                var ads = await _context.Ads
                    .Where(a => a.UserId == userId && a.Status == "active")
                    .Include(a => a.Category)
                    .Include(a => a.Images)
                    .OrderByDescending(a => a.CreatedAt)
                    .ToListAsync();

                var result = ads.Select(ad => new
                {
                    id = ad.Id,
                    title = ad.Title,
                    description = ad.Description,
                    price = ad.Price,
                    category = ad.Category?.Name,
                    location = ad.Location,
                    imageUrl = ad.Images.FirstOrDefault(i => i.IsMain)?.Url ?? ad.ImageUrl,
                    date = ad.CreatedAt.ToString("o"),
                    views = ad.ViewsCount,
                    userId = ad.UserId
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении объявлений пользователя ID {userId}", userId);
                return StatusCode(500, new { error = "Не удалось получить объявления" });
            }
        }

        // Функционал избранного
        [HttpPost("favorites")]
        [Authorize]
        public async Task<IActionResult> AddToFavorites([FromBody] FavoriteRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                // Проверка существования объявления
                var ad = await _context.Ads.FindAsync(request.AdId);
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

                // Создаем Favorite с прикрепленными сущностями
                var favorite = new Favorite
                {
                    UserId = userId,
                    AdId = request.AdId,
                    AddedAt = DateTime.UtcNow,
                    // Явно прикрепляем сущности
                    User = _context.Users.Attach(new User { Id = userId }).Entity,
                    Ad = _context.Ads.Attach(ad).Entity
                };

                _context.Favorites.Add(favorite);
                await _context.SaveChangesAsync();

                return Ok(new { isFavorite = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении в избранное");
                return StatusCode(500, new { error = "Не удалось добавить в избранное" });
            }
        }

        [HttpDelete("favorites/{adId}")]
        [Authorize]
        public async Task<IActionResult> RemoveFromFavorites(int adId)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var favorite = await _context.Favorites
                    .FirstOrDefaultAsync(f => f.UserId == userId && f.AdId == adId);

                if (favorite == null)
                {
                    return NotFound(new { error = "Объявление не найдено в избранном" });
                }

                _context.Favorites.Remove(favorite);
                await _context.SaveChangesAsync();

                return NoContent();//было return Ok(new { isFavorite = false });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении из избранного");
                return StatusCode(500, new { error = "Не удалось удалить из избранного" });
            }
        }

        [HttpGet("favorites/check")]
        [Authorize]
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

        [HttpGet("favorites")]
        [Authorize]
        public async Task<IActionResult> GetFavorites()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var favoriteAdIds = await _context.Favorites
                    .Where(f => f.UserId == userId)
                    .Select(f => f.AdId)
                    .ToListAsync();

                var ads = await _context.Ads
                    .Where(a => favoriteAdIds.Contains(a.Id) && a.Status == "active")
                    .Include(a => a.Images)
                    .Include(a => a.Category) // Важно: включаем категорию
                    .ToListAsync();

                var result = ads.Select(ad => new
                {
                    id = ad.Id,
                    title = ad.Title,
                    price = ad.Price,
                    imageUrl = ad.Images.FirstOrDefault(i => i.IsMain)?.Url ?? ad.ImageUrl,
                    category = ad.Category?.Name ?? "Без категории", // Добавляем категорию
                    location = ad.Location,
                    description = ad.Description // Добавляем описание для поиска
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении избранного");
                return StatusCode(500, new
                {
                    error = "Не удалось получить избранное",
                    details = ex.Message
                });
            }
        }

        // Классы для запросов
        public class AdCreateRequest
        {
            public required string Title { get; set; }
            public required string Description { get; set; }
            public decimal Price { get; set; }
            public int CategoryId { get; set; }
            public required string Location { get; set; }
            public required string ImageUrl { get; set; }
        }

        public class AdUpdateRequest
        {
            public string Title { get; set; }
            public string Description { get; set; }
            public decimal Price { get; set; }
            public int CategoryId { get; set; }
            public string Location { get; set; }
            public string ImageUrl { get; set; }
        }

        public class FavoriteRequest
        {
            public int AdId { get; set; }
        }

        public class ReviewRequest
        {
            public int Rating { get; set; }
            public string Comment { get; set; }
        }
    }
}