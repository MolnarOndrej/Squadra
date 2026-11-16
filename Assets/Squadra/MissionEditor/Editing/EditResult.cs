namespace Squadra.MissionEditing
{
    /// <summary>What one edit did, with the reason when it was refused.</summary>
    public readonly struct EditResult
    {
        EditResult(EditOutcome outcome, string message, string createdId)
        {
            Outcome = outcome;
            Message = message;
            CreatedId = createdId;
        }

        public EditOutcome Outcome { get; }

        /// <summary>Why the edit was refused; null otherwise.</summary>
        public string Message { get; }

        /// <summary>The ID of the node an edit created; null otherwise.</summary>
        public string CreatedId { get; }

        public bool Applied => Outcome == EditOutcome.Applied;
        public bool Rejected => Outcome == EditOutcome.Rejected;

        public static EditResult Done(string createdId = null)
        {
            return new EditResult(EditOutcome.Applied, null, createdId);
        }

        public static readonly EditResult NoChange = new EditResult(EditOutcome.Unchanged, null, null);

        public static EditResult Refused(string message)
        {
            return new EditResult(EditOutcome.Rejected, message, null);
        }
    }
}
