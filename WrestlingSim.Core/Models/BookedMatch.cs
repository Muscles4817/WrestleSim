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

        public string Name => $"{Plan.WrestlerA.RingName} vs {Plan.WrestlerB.RingName}";

        public CardItemKind Kind => CardItemKind.Match;

        // Entrances and the bell either side of the beats themselves.
        public int DurationMinutes =>
            2 + Plan.Beats.Sum(b => b.DurationMinutes);

        public IReadOnlyList<Wrestler> Wrestlers => new[] { Plan.WrestlerA, Plan.WrestlerB };
    }
}
