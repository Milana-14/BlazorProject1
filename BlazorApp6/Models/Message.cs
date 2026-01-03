namespace BlazorApp6.Models
{
    public class Message
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SwapId { get; set; }
        public Guid SenderId { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; }

        public Message() { }
        public Message(Guid id, Guid swapId, Guid senderId, string content)
        {
            Id = id;
            SwapId = swapId;
            SenderId = senderId;
            Content = content;
            IsRead = false;
        }
    }

    public class MessageForChat
    {
        public Guid Id { get; set; }
        public Guid SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public MessageForChat(Guid id, Guid senderId, string senderName, string content, DateTime timestamp)
        {
            Id = id;
            SenderId = senderId;
            SenderName = senderName;
            Content = content;
            Timestamp = timestamp;
        }
    }

    public class AiMessage
    {
        public Guid Id { get; set; }
        public Guid StudentId { get; set; }
        public Guid SenderId { get; set; }
        public string SenderName { get; set; } = "";
        public string Content { get; set; } = "";
        public bool IsFile { get; set; }
        public string? FileName { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
