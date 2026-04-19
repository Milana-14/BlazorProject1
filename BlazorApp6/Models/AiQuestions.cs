namespace BlazorApp6.Models
{
    public class AiQuestions
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SwapId { get; set; }
        public string Question1 { get; set; }
        public string OptionA1 { get; set; }
        public string OptionB1 { get; set; }
        public string OptionC1 { get; set; }
        public int CorrectAnswer1 { get; set; }

        public string Question2 { get; set; }
        public string OptionA2 { get; set; }
        public string OptionB2 { get; set; }
        public string OptionC2 { get; set; }
        public int CorrectAnswer2 { get; set; }

        public string Question3 { get; set; }
        public string OptionA3 { get; set; }
        public string OptionB3 { get; set; }
        public string OptionC3 { get; set; }
        public int CorrectAnswer3 { get; set; }

    }
}
