namespace WrestlingSim.Models.World
{
    /// <summary>
    /// A promotion's booking direction (living-world plan, Part 5): decided once a month and
    /// kept, so it does not re-decide its whole product every week. Weekly cards follow it.
    /// </summary>
    public class BookingDirection
    {
        public DateOnly Decided { get; set; }

        /// <summary>Who it is building: they win more than their standing alone would earn.</summary>
        public List<string> Pushes { get; set; } = new();

        /// <summary>Title name → who is meant to take it at the next big night it is defended.</summary>
        public Dictionary<string, string> NextChampion { get; set; } = new();

        /// <summary>Pushes the audience refused, and when — not tried again for a while.</summary>
        public Dictionary<string, DateOnly> Rejected { get; set; } = new();
    }
}
