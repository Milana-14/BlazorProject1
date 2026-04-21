namespace BlazorApp6.Models
{
    public class AiEvaluation
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SwapId { get; set; }
        public int ExplanationClarity { get; set; }
        public int UnderstandingLevel { get; set; }
    }
}
