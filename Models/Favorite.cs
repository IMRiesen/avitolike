namespace AvitoLike.Models;

public class Favorite
{
    public int UserId { get; set; }
    public int AdId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Ad Ad { get; set; } = null!;
}