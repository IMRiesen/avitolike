namespace AvitoLike.Models;

public class ViewHistory
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public int AdId { get; set; }
    public DateTime ViewedAt { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }

    public User? User { get; set; }
    public Ad Ad { get; set; } = null!;
}