namespace AvitoLike.Models;

public class Review
{
    public int Id { get; set; }
    public int AuthorId { get; set; }
    public int TargetUserId { get; set; }
    public int? AdId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User Author { get; set; } = null!;
    public User TargetUser { get; set; } = null!;
    public Ad? Ad { get; set; }
}