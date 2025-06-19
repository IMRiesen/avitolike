using AvitoLike.Data;
using AvitoLike.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;

namespace AvitoLike.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly ILogger<AuthController> _logger;

        public AuthController(AppDbContext context, IConfiguration config, ILogger<AuthController> logger)
        {
            _context = context;
            _config = config;
            _logger = logger;
        }

        [HttpPost("update-sample-passwords")]
        public async Task<IActionResult> UpdateSamplePasswords()
        {
            var ivan = await _context.Users.FirstOrDefaultAsync(u => u.Email == "ivan@example.com");
            var anna = await _context.Users.FirstOrDefaultAsync(u => u.Email == "anna@example.com");

            if (ivan == null || anna == null)
                return NotFound("Тестовые пользователи не найдены");

            ivan.SetPassword("IvanPassword123!");
            anna.SetPassword("AnnaPassword123!");

            await _context.SaveChangesAsync();
            return Ok("Пароли для Ивана и Анны обновлены");
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                // Проверка уникальности email
                if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                {
                    return BadRequest(new { Message = "Пользователь с таким email уже существует" });
                }

                // Создание нового пользователя
                var user = new User
                {
                    Username = request.Username,
                    Email = request.Email,
                    Phone = request.Phone
                };

                user.SetPassword(request.Password);
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Получаем или создаем роль "User"
                var userRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "User");
                if (userRole == null)
                {
                    userRole = new Role { Name = "User" };
                    _context.Roles.Add(userRole);
                    await _context.SaveChangesAsync();
                }

                // Добавляем связь с ролью
                _context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = userRole.Id });

                // Создаем настройки пользователя
                _context.UserSettings.Add(new UserSetting
                {
                    UserId = user.Id,
                    Theme = "light",
                    NotifyMessages = true,
                    NotifyFavorites = true,
                    NotifyReviews = true
                });

                await _context.SaveChangesAsync();

                _logger.LogInformation("Успешная регистрация: {Email}", user.Email);

                // Формируем ответ
                return Ok(new
                {
                    user.Id,
                    user.Username,
                    user.Email,
                    user.AvatarUrl,
                    Roles = new[] { userRole.Name },
                    Token = GenerateJwtToken(user),
                    CreatedAt = user.CreatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка регистрации для {request.Email}");
                return StatusCode(500, new
                {
                    Message = "Ошибка при регистрации",
                    Error = ex.Message,
                    StackTrace = ex.StackTrace
                });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                    .FirstOrDefaultAsync(u => u.Email == request.Email);

                if (user == null || !user.VerifyPassword(request.Password))
                {
                    _logger.LogWarning("Неудачная попытка входа для {Email}", request.Email);
                    return Unauthorized(new { Message = "Неверный email или пароль" });
                }

                user.UpdateLastLogin();
                await _context.SaveChangesAsync();

                var token = GenerateJwtToken(user);

                _logger.LogInformation("Успешный вход пользователя: {Email}", user.Email);

                return Ok(new
                {
                    user.Id,
                    username = user.Username,
                    user.Email,
                    user.AvatarUrl,
                    Roles = user.UserRoles.Select(ur => ur.Role.Name),
                    Token = token,
                    CreatedAt = user.CreatedAt // ДОБАВЛЕНО
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при входе пользователя");
                return StatusCode(500, new { Message = "Ошибка при входе" });
            }
        }

        [Authorize]
        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentUser()
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                var user = await _context.Users
                    .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    return NotFound(new { Message = "Пользователь не найден" });
                }

                return Ok(new
                {
                    user.Id,
                    username = user.Username,
                    user.Email,
                    user.AvatarUrl,
                    Roles = user.UserRoles.Select(ur => ur.Role.Name)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении текущего пользователя");
                return StatusCode(500, new { Message = "Ошибка при получении пользователя" });
            }
        }

        private string GenerateJwtToken(User user)
        {
            // Получаем ID ролей пользователя
            var roleIds = _context.UserRoles
                .Where(ur => ur.UserId == user.Id)
                .Select(ur => ur.RoleId)
                .ToList();

            // Получаем имена ролей по ID
            var roleNames = _context.Roles
                .AsNoTracking()
                .Where(r => roleIds.Contains(r.Id))
                .Select(r => r.Name)
                .ToList();

            // Если ролей нет, используем роль "User" по умолчанию
            if (roleNames.Count == 0)
            {
                roleNames.Add("User");
                _logger.LogWarning($"No roles found for user {user.Id}, using default role");
            }

            var claims = new[]
            {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.Username),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Role, string.Join(",", roleNames))
    };

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddMinutes(Convert.ToInt32(_config["Jwt:AccessTokenExpireMinutes"])),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public class RegisterRequest
    {
        [Required(ErrorMessage = "Имя пользователя обязательно")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Имя пользователя должно быть от 3 до 50 символов")]
        public string Username { get; set; } = null!;

        [Required(ErrorMessage = "Email обязателен")]
        [EmailAddress(ErrorMessage = "Некорректный формат email")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Пароль обязателен")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Пароль должен быть не менее 8 символов")]
        public string Password { get; set; } = null!;

        [Required(ErrorMessage = "Телефон обязателен")]
        [Phone(ErrorMessage = "Некорректный формат телефона")]
        public string Phone { get; set; } = null!;
    }

        public class LoginRequest
        {
            [Required(ErrorMessage = "Email обязателен")]
            [EmailAddress(ErrorMessage = "Некорректный формат email")]
            public string Email { get; set; } = null!;

            [Required(ErrorMessage = "Пароль обязателен")]
            public string Password { get; set; } = null!;
        }
    }
}