using WrestlingSim.Models.MatchPlan;

namespace WrestlingSim.Models
{
    /// <summary>
    /// A fully-planned match sitting on a show card. Wraps a MatchPlan so the show
    /// layer runs the real beat engine rather than the legacy MatchSimulator.
    /// </summary>
    public class BookedMatch : ICardItem
    {
        public required MatchPlan.MatchPlan Plan { get; init; }

        /// <summary>Name of the structure preset this was built from, for display.</summary>
        public string StructureName { get; init; } = "Custom";

        /// <summary>
        /// How the match is billed. Every side, not the first two — a triple threat billed
        /// "A vs B" leaves out one of the people in it, which is wrong on the card, wrong on
        /// the result screen, and wrong in the show report.
        /// </summary>
        public string Name => string.Join(" vs ", Plan.Sides.Select(s => s.Name));

        public CardItemKind Kind => CardItemKind.Match;

        /// <summary>
        /// What a match of these beats costs the card: the beats, plus the entrances and the
        /// bell either side of them.
        ///
        /// Static so that everything asking "will this fit" reads one definition. The
        /// builder's runtime figure and the brief's over-running warning were both computing
        /// it themselves, and the warning left the two minutes off — so a booker could be
        /// told a match fit and then watch the card overrun by exactly the entrances.
        /// </summary>
        public static int RuntimeOf(IEnumerable<MatchBeat> beats) =>
            2 + beats.Sum(b => b.DurationMinutes);

        /// <summary>
        /// What a planned-but-uncast match is assumed to cost, so the runtime meter is worth
        /// reading while the card is still being laid out. Without it an empty slot costs the
        /// two minutes of its entrances, and a booker planning seven matches would be told
        /// they had used fourteen minutes of a ninety-minute show.
        /// </summary>
        public const int PlannedMinutes = 12;

        public int DurationMinutes =>
            Plan.Beats.Count > 0 ? RuntimeOf(Plan.Beats) : PlannedMinutes;

        /// <summary>
        /// Cast and written. Two sides at least, everybody they are waiting for, and beats to
        /// work — the three things missing from a slot somebody has only planned.
        /// </summary>
        public bool IsComplete =>
            Plan.Sides.Count >= 2 && Plan.Sides.All(s => s.IsComplete) && Plan.Beats.Count > 0;

        public IReadOnlyList<Wrestler> Wrestlers => Plan.AllParticipants.ToList();
    }
}
