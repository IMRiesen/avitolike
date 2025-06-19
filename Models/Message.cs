namespace AvitoLike.Models;

public class Message
{
    public int Id { get; set; }
    public int SenderId { get; set; }
    public int ReceiverId { get; set; }
    public int? AdId { get; set; }
    public string Content { get; set; } = null!;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; } = false;

    public User Sender { get; set; } = null!;
    public User Receiver { get; set; } = null!;
    public Ad? Ad { get; set; }
}