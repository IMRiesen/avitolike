namespace AvitoLike.Models;

public class AdImage
{
    public int Id { get; set; }
    public int AdId { get; set; }
    public string Url { get; set; } = null!;
    public bool IsMain { get; set; } = false;
    public DateTime UploadDate { get; set; } = DateTime.UtcNow;

    public Ad Ad { get; set; } = null!;
}