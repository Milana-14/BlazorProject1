namespace BlazorApp6.Models
{
    public class AiMessage
    {
        public Guid Id { get; set; }
        public Guid SenderId { get; set; }
        public string SenderName { get; set; }
        public string Content { get; set; }
        public bool IsFile { get; set; } = false;
        public string? FileName { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
