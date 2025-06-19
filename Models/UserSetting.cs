using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AvitoLike.Models
{
    public class UserSetting
    {
        [Key]
        [ForeignKey("User")]
        public int UserId { get; set; }

        public bool NotifyMessages { get; set; } = true;
        public bool NotifyFavorites { get; set; } = true;
        public bool NotifyReviews { get; set; } = true;
        public string Theme { get; set; } = "light";

        public User User { get; set; } = null!;
    }
}