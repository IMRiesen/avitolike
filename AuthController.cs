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
                return NotFound("�������� ������������ �� �������");

            ivan.SetPassword("IvanPassword123!");
            anna.SetPassword("AnnaPassword123!");

            await _context.SaveChangesAsync();
            return Ok("������ ��� ����� � ���� ���������");
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                // �������� ������������ email
                if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                {
                    return BadRequest(new { Message = "������������ � ����� email ��� ����������" });
                }

                // �������� ������ ������������
                var user = new User
                {
                    Username = request.Username,
                    Email = request.Email,
                    Phone = request.Phone
                };

                user.SetPassword(request.Password);
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // �������� ��� ������� ���� "User"
                var userRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "User");
                if (userRole == null)
                {
                    userRole = new Role { Name = "User" };
                    _context.Roles.Add(userRole);
                    await _context.SaveChangesAsync();
                }

                // ��������� ����� � �����
                _context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = userRole.Id });

                // ������� ��������� ������������
                _context.UserSettings.Add(new UserSetting
                {
                    UserId = user.Id,
                    Theme = "light",
                    NotifyMessages = true,
                    NotifyFavorites = true,
                    NotifyReviews = true
                });

                await _context.SaveChangesAsync();

                _logger.LogInformation("�������� �����������: {Email}", user.Email);

                // ��������� �����
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
                _logger.LogError(ex, $"������ ����������� ��� {request.Email}");
                return StatusCode(500, new
                {
                    Message = "������ ��� �����������",
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
                    _logger.LogWarning("��������� ������� ����� ��� {Email}", request.Email);
                    return Unauthorized(new { Message = "�������� email ��� ������" });
                }

                user.UpdateLastLogin();
                await _context.SaveChangesAsync();

                var token = GenerateJwtToken(user);

                _logger.LogInformation("�������� ���� ������������: {Email}", user.Email);

                return Ok(new
                {
                    user.Id,
                    username = user.Username,
                    user.Email,
                    user.AvatarUrl,
                    Roles = user.UserRoles.Select(ur => ur.Role.Name),
                    Token = token,
                    CreatedAt = user.CreatedAt // ���������
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "������ ��� ����� ������������");
                return StatusCode(500, new { Message = "������ ��� �����" });
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
                    return NotFound(new { Message = "������������ �� ������" });
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
                _logger.LogError(ex, "������ ��� ��������� �������� ������������");
                return StatusCode(500, new { Message = "������ ��� ��������� ������������" });
            }
        }

        private string GenerateJwtToken(User user)
        {
            // �������� ID ����� ������������
            var roleIds = _context.UserRoles
                .Where(ur => ur.UserId == user.Id)
                .Select(ur => ur.RoleId)
                .ToList();

            // �������� ����� ����� �� ID
            var roleNames = _context.Roles
                .AsNoTracking()
                .Where(r => roleIds.Contains(r.Id))
                .Select(r => r.Name)
                .ToList();

            // ���� ����� ���, ���������� ���� "User" �� ���������
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
        [Required(ErrorMessage = "��� ������������ �����������")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "��� ������������ ������ ���� �� 3 �� 50 ��������")]
        public string Username { get; set; } = null!;

        [Required(ErrorMessage = "Email ����������")]
        [EmailAddress(ErrorMessage = "������������ ������ email")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "������ ����������")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "������ ������ ���� �� ����� 8 ��������")]
        public string Password { get; set; } = null!;

        [Required(ErrorMessage = "������� ����������")]
        [Phone(ErrorMessage = "������������ ������ ��������")]
        public string Phone { get; set; } = null!;
    }

        public class LoginRequest
        {
            [Required(ErrorMessage = "Email ����������")]
            [EmailAddress(ErrorMessage = "������������ ������ email")]
            public string Email { get; set; } = null!;

            [Required(ErrorMessage = "������ ����������")]
            public string Password { get; set; } = null!;
        }
    }
}