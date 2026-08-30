namespace WrestlingSim.Models
{
    public class Show
    {
        public string Name { get; set; } = "Unnamed Show";
        public DateTime Date { get; set; }
        public string Location { get; set; } = "Unknown Arena";
        public int AudienceSize { get; set; } = 10000;

        /// <summary>Matches and segments in running order.</summary>
        public List<ICardItem> Card { get; set; } = new();

        /// <summary>The runtime the card has to fit inside.</summary>
        public int TotalDurationMinutes { get; set; } = 180;

        // ── Runtime budget ───────────────────────────────────────────────────

        public int BookedMinutes => Card.Sum(i => i.DurationMinutes);

        public int RemainingMinutes => TotalDurationMinutes - BookedMinutes;

        public bool IsOverrunning => BookedMinutes > TotalDurationMinutes;

        /// <summary>
        /// How far over the runtime the card is, as a fraction of the budget.
        /// Drives the overrun penalty in ShowSimulator.
        /// </summary>
        public double OverrunFraction => TotalDurationMinutes <= 0
            ? 0
            : Math.Max(0, BookedMinutes - TotalDurationMinutes) / (double)TotalDurationMinutes;
    }
}
