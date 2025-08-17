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
        public event Action<Match>? OnConfirmed;
        public event Action<Match>? OnRejected;
        public event Action<Match>? OnCanceled;

        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid Student1Id { get; }
        public Guid Student2Id { get; }
        public MatchStatus Status { get; set; }
        public DateTime DateRequested { get; private set; }
        public DateTime? DateConfirmed { get; private set; }

        public Match(Student student1, Student student2)
        {
            this.Student1Id = student1.Id;
            this.Student2Id = student2.Id;
            DateRequested = DateTime.Now;
            Status = MatchStatus.Pending;
        }

        public void Confirm()
        {
            if (Status != MatchStatus.Pending)
                throw new InvalidOperationException("Матч уже обработан.");

            Status = MatchStatus.Confirmed;
            DateConfirmed = DateTime.Now;

            OnConfirmed?.Invoke(this);
        }

        public void Reject()
        {
            if (Status != MatchStatus.Pending)
                throw new InvalidOperationException("Матч уже обработан.");

            Status = MatchStatus.Rejected;

            OnRejected?.Invoke(this);
        }

        public void Cancel()
        {
            if (Status != MatchStatus.Pending)
                throw new InvalidOperationException("Матч уже обработан.");

            Status = MatchStatus.Canceled;

            OnCanceled?.Invoke(this);
        }
    }
}
