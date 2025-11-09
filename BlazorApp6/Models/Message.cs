namespace BlazorApp6.Models
{
    public class Message
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SwapId { get; set; }
        public Guid SenderId { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public Message() { }
        public Message(Guid swapId, Guid senderId, string content)
        {
            SwapId = swapId;
            SenderId = senderId;
            Content = content;
        }
    }

    public class MessageForChat
    {
        public string SenderName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public MessageForChat(string senderName, string content, DateTime timestamp)
        {
            SenderName = senderName;
            Content = content;
            Timestamp = timestamp;
        }
    }
}
