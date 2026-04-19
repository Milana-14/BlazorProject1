namespace BlazorApp6.Models
{
    public class AiEvaluation
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SwapId { get; set; }
        public double ExplanationClarity { get; set; }
        public double UnderstandingLevel { get; set; }
    }
}
