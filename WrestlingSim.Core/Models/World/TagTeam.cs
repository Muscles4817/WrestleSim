namespace WrestlingSim.Models.World
{
    /// <summary>
    /// A standing tag team: two or more people the audience reads as one act.
    ///
    /// The reason this exists as an entity rather than as a pair of wrestlers put on the
    /// same side is <see cref="Chemistry"/>. Two singles wrestlers thrown together and a
    /// team that has worked two hundred matches together are not the same thing, and the
    /// difference is not in either man's stats — it is in the pair
    /// (docs/wrestling-reference/18-match-craft.md §3,
    /// docs/wrestling-reference/12-pushes-and-positioning.md §2.2.1).
    ///
    /// Chemistry does two jobs, deliberately opposite in sign:
    ///
    ///   • It makes tandem offence work. A double team is only as good as the pair
    ///     executing it, and an established team executes it better than the sum of two
    ///     good workers.
    ///
    ///   • It makes the team read to the crowd as *one act* rather than two people, which
    ///     is what lets a strong partner carry a weak one. Mechanically it lowers
    ///     <c>Ctx.DragWeight</c>, so the side reads closer to its best member.
    ///
    /// And it decays, like everything else that is good in this business. A team that
    /// stops working together stops being a team.
    /// </summary>
    public class TagTeam
    {
        /// <summary>Stable id so saves and card items can refer to this team.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Name { get; set; } = "Tag Team";

        /// <summary>The members, in billing order.</summary>
        public List<Wrestler> Members { get; set; } = new();

        /// <summary>When the team formed. Tenure is measured from here.</summary>
        public DateOnly Formed { get; set; }

        /// <summary>Set when the team splits. A disbanded team keeps its record.</summary>
        public DateOnly? Disbanded { get; set; }

        public bool IsActive => Disbanded is null;

        /// <summary>Matches worked together. The thing chemistry is actually built out of.</summary>
        public int MatchesTogether { get; set; }

        /// <summary>The last night they teamed. Chemistry decays from here.</summary>
        public DateOnly? LastTeamed { get; set; }

        // ── Chemistry ────────────────────────────────────────────────────────

        /// <summary>
        /// 0–1. How well this pair works as a pair, independent of how good either of them
        /// is alone. Stored rather than derived from <see cref="MatchesTogether"/> so that
        /// a team assembled from two people who already had history can start above zero,
        /// and so decay is a real event rather than a recomputation.
        /// </summary>
        public double Chemistry { get; set; }

        /// <summary>Matches together before a team reads as fully established.</summary>
        public const int MatchesToEstablish = 25;

        /// <summary>
        /// Chemistry kept per day apart. ~0.9985 is a little under a two-year half-life:
        /// a team that stops teaming does not become strangers overnight, but a reunion
        /// five years later is not the same act that split up.
        /// </summary>
        public const double DailyRetention = 0.9985;

        /// <summary>Days off before chemistry starts slipping at all.</summary>
        public const int GraceDays = 60;

        /// <summary>
        /// What one more match together is worth. Deliberately saturating: the first ten
        /// matches build most of it and the hundredth builds almost nothing, which is the
        /// same shape every other accumulation in the engine uses.
        /// </summary>
        public void RecordMatch(DateOnly? date)
        {
            MatchesTogether++;
            if (date is { } d)
            {
                LastTeamed = d;
                // Teaming again restarts the clock, so the next idle stretch is measured
                // from this match rather than from wherever decay had got to.
                DecayedTo  = null;
            }

            double target = Math.Min(1.0, MatchesTogether / (double)MatchesToEstablish);

            // Move a share of the remaining distance rather than jumping to the target, so
            // a team that formed, split and reformed does not snap straight back.
            Chemistry = Math.Clamp(Chemistry + (target - Chemistry) * 0.35, 0, 1);
        }

        /// <summary>
        /// The last day decay was charged for. Without this the world clock re-charged the
        /// *whole* idle period every single day, compounding to 0.9985^(N(N+1)/2) rather
        /// than 0.9985^N — a half-life of about thirty days instead of the intended two
        /// years, and effectively zero within five months.
        /// </summary>
        public DateOnly? DecayedTo { get; set; }

        /// <summary>
        /// Applies decay for time spent apart. Called by the world clock, not by the
        /// engine — chemistry is a property of the team's history, not of any one match.
        ///
        /// Idempotent per day: calling it twice for the same date charges once, so it does
        /// not matter how many times a day the clock ticks it.
        /// </summary>
        public void Decay(DateOnly today)
        {
            if (LastTeamed is not { } last) return;

            // Charge only the days not already charged. The grace period is measured from
            // the last match, so decay begins at last + GraceDays.
            var from = DecayedTo ?? last.AddDays(GraceDays);

            // Nothing charged yet, and deliberately no marker set: stamping DecayedTo while
            // still inside the grace period would make the next call measure from today
            // rather than from the end of the grace, charging the grace days themselves.
            if (today <= from) return;

            int days = today.DayNumber - from.DayNumber;
            Chemistry = Math.Clamp(Chemistry * Math.Pow(DailyRetention, days), 0, 1);
            DecayedTo = today;
        }

        public bool Contains(Wrestler w) => Members.Contains(w);

        /// <summary>How long they have been a team, for display.</summary>
        public string TenureLabel(DateOnly today)
        {
            int days = Math.Max(0, (Disbanded ?? today).DayNumber - Formed.DayNumber);
            return days switch
            {
                < 60   => $"{days} days",
                < 365  => $"{days / 30} months",
                _      => $"{days / 365} year{(days / 365 == 1 ? "" : "s")}"
            };
        }

        /// <summary>A plain-English reading of <see cref="Chemistry"/>.</summary>
        public string ChemistryLabel => Chemistry switch
        {
            >= 0.85 => "Move as one",
            >= 0.60 => "Well drilled",
            >= 0.35 => "Getting there",
            >= 0.15 => "Still finding it",
            _       => "Two singles wrestlers"
        };
    }
}
