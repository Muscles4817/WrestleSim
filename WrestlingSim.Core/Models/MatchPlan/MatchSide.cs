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
        /// The standing team these people are, if they are one. Null for two singles
        /// wrestlers thrown together, which is a meaningfully different act — see
        /// <see cref="World.TagTeam"/>.
        /// </summary>
        public World.TagTeam? Team { get; set; }

        /// <summary>
        /// 0–1. How well this side works as a unit. A side of one is trivially in sync
        /// with itself; an ad-hoc pairing has nothing.
        /// </summary>
        public double Chemistry =>
            Members.Count <= 1 ? 1.0 : Team?.Chemistry ?? 0.0;

        /// <summary>
        /// Index into <see cref="Members"/> of whoever begins the match for this side.
        /// The rest start on the apron.
        /// </summary>
        public int StartingIndex { get; init; }

        public int Size => Members.Count;

        /// <summary>
        /// How many people this side is *meant* to hold, while the card is still being
        /// planned. Null once the side is simply whoever is in it.
        ///
        /// This is what lets a card carry a match nobody has cast yet. A booker plans the
        /// shape of a show first — five matches and four segments, in an order — and fills it
        /// in afterwards, which is both how bookers work and the only way to see the shape of
        /// a night before it is finished. Before this, a match had to be built to completion
        /// the moment it was added, so you could not discover you wanted five of them until
        /// you had finished the first.
        ///
        /// Nullable rather than defaulted to one, because every side built by the engine, the
        /// tests and the old builder is "as many as are in it" — and a default of one would
        /// have declared every tag team on every existing card to be half-empty.
        /// </summary>
        public int? Intended { get; set; }

        /// <summary>
        /// Whether this side has the people it is waiting for. A side with no intended size
        /// is complete as soon as somebody is on it.
        /// </summary>
        public bool IsComplete =>
            Intended is { } wanted ? Members.Count >= wanted && wanted > 0 : Members.Count > 0;

        /// <summary>Empty places left on this side, for the card to draw.</summary>
        public int Vacancies => Math.Max(0, (Intended ?? Members.Count) - Members.Count);

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
