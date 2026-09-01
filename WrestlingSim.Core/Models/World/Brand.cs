namespace WrestlingSim.Models.World
{
    /// <summary>
    /// One half of a brand split: an exclusive roster, its own shows, its own identity.
    ///
    /// The performers all work for the same company; the fiction is that the brands are
    /// separate competing entities (docs/wrestling-reference/22-brand-splits.md §1).
    /// Everything that makes that fiction hold — exclusivity, distinct identity, equal
    /// investment — is enforced or measured elsewhere; this is the entity it hangs off.
    /// </summary>
    public class Brand
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Name { get; set; } = "New Brand";

        /// <summary>
        /// One line on what this brand is for. Doc 22 §3.2: two brands that feel identical
        /// add administrative complexity and no audience-facing value. The sim cannot
        /// grade prose, so this is the player's own note — but asking for it at all is the
        /// point at which they have to decide the brands are different.
        /// </summary>
        public string Identity { get; set; } = "";

        /// <summary>Accent colour, so the two brands are told apart at a glance.</summary>
        public string Colour { get; set; } = "#d4af37";

        /// <summary>
        /// Who belongs to this brand, by <see cref="Models.Wrestler.Id"/>.
        ///
        /// By id and never by value, for the same reason the save format is: the object
        /// graph shares wrestler references and a second copy would break identity.
        /// </summary>
        public HashSet<string> RosterIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Overall ratings of the last few shows this brand ran, oldest first.
        ///
        /// This is what makes the B-show spiral observable rather than asserted: a brand
        /// that is booked worse rates worse, and a brand that rates worse makes stars more
        /// slowly (doc 22 §4.2).
        /// </summary>
        public List<double> RecentRatings { get; set; } = new();

        /// <summary>How many shows of form are kept. Long enough to be a trend, short enough to move.</summary>
        public const int FormWindow = 8;

        public void RecordShow(double overallRating)
        {
            RecentRatings.Add(overallRating);
            while (RecentRatings.Count > FormWindow) RecentRatings.RemoveAt(0);
        }

        /// <summary>Average of the kept ratings, or null before this brand has run anything.</summary>
        public double? Form => RecentRatings.Count == 0 ? null : RecentRatings.Average();

        public bool Contains(string wrestlerId) => RosterIds.Contains(wrestlerId);

        public override string ToString() => $"{Name} ({RosterIds.Count})";
    }
}
