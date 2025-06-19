using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AvitoLike.Models;

public class Ad
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("title")]
    public string Title { get; set; } = null!;

    [Required]
    [Column("description")]
    public string Description { get; set; } = null!;

    [Required]
    [Column("price")]
    public decimal Price { get; set; }

    [Required]
    [Column("category_id")]
    public int CategoryId { get; set; }

    [Required]
    [Column("location")]
    public string Location { get; set; } = null!;

    [Column("image_url")]
    public string ImageUrl { get; set; } = "/images/placeholder.jpg";

    [Required]
    [Column("user_id")]
    public int UserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("views_count")]
    public int ViewsCount { get; set; } = 0;

    [Column("status")]
    public string Status { get; set; } = "active";

    // Navigation properties
    public User User { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public List<AdImage> Images { get; set; } = new();
    public List<Favorite> Favorites { get; set; } = new();
    public List<Review> Reviews { get; set; } = new();
}