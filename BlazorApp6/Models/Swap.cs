namespace BlazorApp6.Models
{
    public enum SwapStatus
    {
        Pending,
        Confirmed,
        Rejected,
        PendingCompleted,
        CompletedNotRated,
        Completed
    }

    public class Swap
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid Student1Id { get; set; } // този, който иска помощ
        public Guid Student2Id { get; set; } // този, който ще помогне
        public Guid RequesterId { get; set; }
        public SubjectEnum SubjectForHelp { get; set; }
        public SwapStatus Status { get; set; }
        public DateTime DateRequested { get; set; }
        public DateTime? DateConfirmed { get; set; }

        public void Confirm()
        {
            if (Status != SwapStatus.Pending)
                throw new InvalidOperationException("Свапът вече е обработен.");

            Status = SwapStatus.Confirmed;
            DateConfirmed = DateTime.UtcNow;
        }

        public void Reject()
        {
            if (Status != SwapStatus.Pending)
                throw new InvalidOperationException("Свапът вече е обработен.");

            Status = SwapStatus.Rejected;
        }

        public void ProposeCompletion()
        {
            if (Status != SwapStatus.Confirmed)
                throw new InvalidOperationException("Този свап още не съществува.");

            Status = SwapStatus.PendingCompleted;
        }

        public void CompleteSwap()
        {
            if (Status != SwapStatus.PendingCompleted)
                throw new InvalidOperationException("Този свап не е в статус \"Предложено завършване\".");

            Status = SwapStatus.Completed;
        }

        public void RejectCompletion()  
        {
            if (Status != SwapStatus.PendingCompleted)
                throw new InvalidOperationException("Този свап не е в статус \"Предложено завършване\".");

            Status = SwapStatus.Confirmed;
        }
    }
}
