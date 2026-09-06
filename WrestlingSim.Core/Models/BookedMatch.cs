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

        // Entrances and the bell either side of the beats themselves.
        public int DurationMinutes =>
            2 + Plan.Beats.Sum(b => b.DurationMinutes);

        public IReadOnlyList<Wrestler> Wrestlers => Plan.AllParticipants.ToList();
    }
}
