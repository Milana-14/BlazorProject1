namespace BlazorApp6.Models
{
    public class ModerationResult
    {
        public double Toxic { get; set; }
        public double Error { get; set; }
        public string Suggestion { get; set; }

        public ModerationResult() { }
        public ModerationResult(double toxic, double error, string suggestion)
        {
            Toxic = toxic;
            Error = error;
            Suggestion = suggestion;
        }
    }
}
