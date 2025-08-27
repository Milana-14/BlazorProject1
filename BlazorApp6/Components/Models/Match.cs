namespace BlazorApp6.Components.Models
{
    public enum MatchStatus
    {
        Pending,
        Confirmed,
        Rejected,
        Unpaired
    }

    public class Match
    {
        public event Action<Match>? OnConfirmed;
        public event Action<Match>? OnRejected;
        public event Action<Match>? OnUnpaired;

        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid Student1Id { get; set; }
        public Guid Student2Id { get; set; }
        public MatchStatus Status { get; set; }
        public DateTime DateRequested { get; set; }
        public DateTime? DateConfirmed { get; set; }

        public void Confirm()
        {
            if (Status != MatchStatus.Pending)
                throw new InvalidOperationException("Матчът вече е обработен.");

            Status = MatchStatus.Confirmed;
            DateConfirmed = DateTime.Now;

            OnConfirmed?.Invoke(this);
        }

        public void Reject()
        {
            if (Status != MatchStatus.Pending)
                throw new InvalidOperationException("Матчът вече е обработен.");

            Status = MatchStatus.Rejected;

            OnRejected?.Invoke(this);
        }

        public void Unpair()
        {
            if (Status != MatchStatus.Confirmed)
                throw new InvalidOperationException("Този матч още не существува.");

            Status = MatchStatus.Unpaired;

            OnUnpaired?.Invoke(this);
        }
    }
}
