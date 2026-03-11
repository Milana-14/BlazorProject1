namespace BlazorApp6.Models
{
    public class ModerationResult
    {
        public Guid MessageId { get; set; }
        public double Toxic { get; set; }
        public double Error { get; set; }
        public string Suggestion { get; set; }

        public ModerationResult() { }
        public ModerationResult(double toxic, double error, string suggestion)
        {
            MessageId = Guid.Empty;
            Toxic = toxic;
            Error = error;
            Suggestion = suggestion;
        }
        public ModerationResult(Guid messageId, double toxic, double error, string suggestion)
        {
            MessageId = messageId;
            Toxic = toxic;
            Error = error;
            Suggestion = suggestion;
        }
    }
}
