namespace WrestlingSim.Models.MatchPlan
{
    /// <summary>
    /// One side of a match: a singles wrestler, a tag team, or a larger unit.
    ///
    /// The engine reasons about sides rather than about two named people because almost
    /// everything it already models is side-level. Who is on top right now, who the crowd
    /// wants to see win, who the finish is booked for — none of those become a different
    /// question when there are two people in the corner instead of one.
    ///
    /// What *is* new with more than one member is that only one of them is legal at a time.
    /// That is deliberately not stored here: a <see cref="MatchPlan"/> is a booking, and a
    /// booking can be executed more than once (the test suite re-runs the same plan to
    /// compare bookings). Who is currently in the ring is live match state and belongs to
    /// <see cref="Engine.MatchEngineState"/>. This type only says who *starts*.
    /// </summary>
    public sealed class MatchSide
    {
        public List<Wrestler> Members { get; init; } = new();

        /// <summary>
        /// Index into <see cref="Members"/> of whoever begins the match for this side.
        /// The rest start on the apron.
        /// </summary>
        public int StartingIndex { get; init; }

        public int Size => Members.Count;

        /// <summary>True once there is someone on the apron to tag.</summary>
        public bool IsTag => Members.Count > 1;

        /// <summary>
        /// Who takes the opening bell for this side.
        ///
        /// This deliberately does not clamp an out-of-range <see cref="StartingIndex"/>.
        /// Clamping made <see cref="MatchPlan.Validate"/> report a plan as valid that the
        /// engine then crashed on, because the engine indexes <see cref="Members"/> with
        /// the raw value. A wrong index is a booking error and is reported as one.
        /// </summary>
        public Wrestler Starter =>
            Members.Count == 0
                ? throw new InvalidOperationException("This side has nobody in it.")
                : StartingIndex >= 0 && StartingIndex < Members.Count
                    ? Members[StartingIndex]
                    : throw new InvalidOperationException(
                        $"StartingIndex {StartingIndex} is outside this side's {Members.Count} member(s).");

        /// <summary>"Rhea Ripley" for a singles side, "Rhea Ripley &amp; Liv Morgan" for a team.</summary>
        public string Name => string.Join(" & ", Members.Select(m => m.RingName));

        public bool Contains(Wrestler w) => Members.Contains(w);

        /// <summary>Everyone on this side except <paramref name="w"/>.</summary>
        public IEnumerable<Wrestler> PartnersOf(Wrestler w) => Members.Where(m => m != w);

        public static MatchSide Of(params Wrestler[] members) =>
            new() { Members = members.ToList() };
    }
}
