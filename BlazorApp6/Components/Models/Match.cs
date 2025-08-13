namespace BlazorApp6.Components.Models
{
    public enum MatchStatus
    {
        Pending,
        Confirmed,
        Rejected,
        Canceled
    }

    public class Match
    {
        public Guid Student1Id { get; set; }
        public Guid Student2Id { get; set; }
        public MatchStatus Status { get; set; }
        public DateTime DateRequested { get; private set; }
        public DateTime? DateConfirmed { get; private set; }

        public Match(Student student, User partner)
        {
            this.Student1Id = student.Id;
            this.Student2Id = partner.Id;
            DateRequested = DateTime.Now;
            Status = MatchStatus.Pending;
        }

        public void Confirm()
        {
            if (Status != MatchStatus.Pending)
                throw new InvalidOperationException("Матч уже обработан.");
            Status = MatchStatus.Confirmed;
            DateConfirmed = DateTime.Now;
        }

        public void Reject()
        {
            if (Status != MatchStatus.Pending)
                throw new InvalidOperationException("Матч уже обработан.");
            Status = MatchStatus.Rejected;
        }

        public void Cancel()
        {
            if (Status != MatchStatus.Pending)
                throw new InvalidOperationException("Матч уже обработан.");
            Status = MatchStatus.Canceled;
        }
    }
}
