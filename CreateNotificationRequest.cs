namespace AvitoLike.Models
{
    public class CreateNotificationRequest
    {
        public int UserId { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string Type { get; set; }
        public int? RelatedId { get; set; }
    }
}