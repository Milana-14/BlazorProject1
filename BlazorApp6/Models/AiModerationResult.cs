namespace BlazorApp6.Models
{
    public class AiModerationMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid MessageId { get; set; }
        public double Toxic { get; set; }
        public double FactualError { get; set; }
        public string? Suggestion { get; set; }
    }
}
