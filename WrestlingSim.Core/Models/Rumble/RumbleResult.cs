namespace WrestlingSim.Models.Rumble
{
    /// <summary>What a battle royal did, in the terms the format is actually about.</summary>
    public class RumbleResult
    {
        public required Wrestler Winner { get; init; }

        /// <summary>Everybody who went out, in order.</summary>
        public List<RumbleElimination> Eliminations { get; init; } = new();

        /// <summary>
        /// Who lasted longest, and how many entries they outlasted. The format's best story
        /// and the one nobody can book — see <see cref="Engine.RumbleScoring.IronManShare"/>.
        /// </summary>
        public Wrestler? IronMan { get; init; }
        public int IronManOutlasted { get; init; }

        /// <summary>The moments that actually landed, in the order they landed.</summary>
        public List<string> Highlights { get; init; } = new();

        public List<string> Commentary { get; init; } = new();

        // ── Scoring (see RumbleScoring — no technical component, deliberately) ──

        public double MomentScore   { get; init; }
        public double FieldStarPower{ get; init; }
        public double EntryStory    { get; init; }
        public double IronManShare  { get; init; }

        public double FinalScore    { get; init; }
        public double StarRating    => Math.Clamp(FinalScore / 20.0, 0, 5);

        /// <summary>Largest-first, for display.</summary>
        public IEnumerable<(string Label, double Points)> Ordered =>
            new[]
            {
                ("Moments",     MomentScore    * 50.0),
                ("The field",   FieldStarPower * 30.0),
                ("Entry story", EntryStory     * 12.0),
                ("Iron man",    IronManShare   *  8.0)
            }
            .Where(x => x.Item2 > 0.005)
            .OrderByDescending(x => x.Item2);
    }

    /// <summary>One wrestler going over the top rope.</summary>
    public class RumbleElimination
    {
        public required Wrestler Wrestler { get; init; }

        /// <summary>Who dumped them. Null when several went at once and nobody owns it.</summary>
        public Wrestler? By { get; init; }

        /// <summary>Which elimination this was, 1-based.</summary>
        public int Order { get; init; }

        /// <summary>How many were left in the ring afterwards.</summary>
        public int Remaining { get; init; }
    }
}
