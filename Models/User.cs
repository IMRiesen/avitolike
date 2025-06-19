using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AvitoLike.Services;

namespace AvitoLike.Models
{
    [Table("users")]
    public class User
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("username")]
        [MaxLength(50)]
        public string Username { get; set; } = null!;

        [Required]
        [Column("email")]
        [MaxLength(100)]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        [Column("password_hash")]
        [MaxLength(255)]
        public string PasswordHash { get; set; } = null!;

        [Column("phone")]
        [MaxLength(20)]
        public string? Phone { get; set; }

        [Column("avatar_url")]
        [MaxLength(255)]
        public string? AvatarUrl { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("last_login")]
        public DateTime? LastLogin { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        // Навигационные свойства
        public UserSetting? Settings { get; set; }
        public List<UserRole> UserRoles { get; set; } = new();
        public List<Ad> Ads { get; set; } = new();
        public List<Favorite> Favorites { get; set; } = new();

        public void SetPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Пароль не может быть пустым");

            if (password.Length < 8)
                throw new ArgumentException("Пароль должен содержать минимум 8 символов");

            PasswordHash = PasswordHasher.Hash(password);
        }

        public bool VerifyPassword(string password)
        {
            return PasswordHasher.Verify(password, PasswordHash);
        }

        public void UpdateLastLogin()
        {
            LastLogin = DateTime.UtcNow;
        }
    }
}