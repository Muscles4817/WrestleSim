namespace WrestlingSim.Models.MatchPlan
{
    public class MatchEngineResult
    {
        /// <summary>
        /// The person who actually scored the fall — pinned, submitted or was awarded the
        /// decision. In a tag match this is whoever was legal for the winning side when
        /// the finish landed, not the whole team.
        /// </summary>
        public required Wrestler Winner { get; init; }

        /// <summary>The person who was beaten. In a tag match, the man who ate the fall.</summary>
        public required Wrestler Loser  { get; init; }

        /// <summary>Everyone on the winning side, including anyone who never got in.</summary>
        public IReadOnlyList<Wrestler> WinningSide { get; init; } = [];

        /// <summary>Everyone on the losing side.</summary>
        public IReadOnlyList<Wrestler> LosingSide { get; init; } = [];

        /// <summary>Reads better than <see cref="Winner"/> where the fall itself is the point.</summary>
        public Wrestler Pinner => Winner;

        /// <summary>Reads better than <see cref="Loser"/> where the fall itself is the point.</summary>
        public Wrestler Pinned => Loser;

        /// <summary>True when either side had more than one member.</summary>
        public bool WasTagMatch => WinningSide.Count > 1 || LosingSide.Count > 1;

        public List<BeatResult> BeatResults { get; init; } = new();

        // Accumulated scores (raw, pre-normalisation)
        public double TechnicalScore     { get; init; }
        public double StorytellingScore  { get; init; }
        public double CrowdPeakEnergy    { get; init; }
        public double CrowdAverageEnergy { get; init; }
        public double FinishQuality      { get; init; }

        /// <summary>
        /// 0–1. How much of the plan suited the declared MatchType. Always 1.0 for Standard,
        /// which has no preference. Feeds a small bonus/penalty in the final score.
        /// </summary>
        public double MatchTypeCoherence { get; init; } = 1.0;

        /// <summary>
        /// What the room actually did, as a profile rather than a level — see
        /// <see cref="CrowdReaction"/>. The crowd component of the rating is scaled by
        /// <see cref="CrowdReaction.Investment"/>, so a loud disengaged match now grades
        /// below a quiet invested one.
        /// </summary>
        public CrowdReaction Reaction { get; init; } = new();

        /// <summary>A plain-English reading of what the crowd was like.</summary>
        public string CrowdNote => Reaction.Label;

        /// <summary>
        /// 0–1. How much the crowd still wanted to see this specific pairing, 1.0 being
        /// the first time they had seen it. Below 1.0 the room was flatter than the work
        /// deserved — docs/wrestling-reference/20-storylines-and-feuds.md §9.1.
        /// </summary>
        public double Familiarity { get; init; } = 1.0;

        /// <summary>A plain-English reading of <see cref="Familiarity"/>, or null when fresh.</summary>
        public string? StalenessNote => Familiarity switch
        {
            >= 0.99 => null,
            >= 0.88 => "The crowd has seen this before, but they are still up for it.",
            >= 0.75 => "A pairing the crowd knows well. Some of the novelty has gone.",
            >= 0.60 => "They have seen this too often. The room never really came alive.",
            _       => "Nobody needed to see this again, and the building said so."
        };

        // Final rating
        /// <summary>
        /// How the 0–100 <see cref="FinalScore"/> was actually assembled.
        ///
        /// Reported rather than kept private for two reasons. The player one: a rating with
        /// no breakdown is a verdict, not feedback — a booker who cannot see that a match
        /// lost four points on variety cannot learn to book a better one. The engineering
        /// one: it makes the composite *testable*. Every claim about what a term does to a
        /// rating had to be made through the star rating before this, which is the sum of
        /// six things and so proves nothing about any one of them — and review found
        /// exactly that hiding a term that had been deleted without a single test noticing.
        /// </summary>
        public ScoreBreakdown Breakdown { get; init; } = new();

        public double FinalScore  { get; init; }  // 0–100
        public double StarRating  { get; init; }  // 0–5

        // ── Display helpers ──────────────────────────────────────────────────

        public string StarDisplay => $"{GlyphsFor(StarRating)}  ({StarRating:F2} / 5.00)";

        /// <summary>
        /// Star glyphs for a 0–5 rating, rounded to the nearest quarter star.
        /// Shared so every front end renders a rating the same way.
        /// </summary>
        public static string GlyphsFor(double rating)
        {
            int full = (int)rating;
            double rem = rating - full;

            string stars = new string('★', full);
            stars += rem switch
            {
                >= 0.875 => "★",
                >= 0.625 => "¾",
                >= 0.375 => "½",
                >= 0.125 => "¼",
                _        => ""
            };

            // If full+remainder rounded up past 5, cap display
            if (stars.Replace("¼", "").Replace("½", "").Replace("¾", "").Length > 5)
                stars = "★★★★★";

            return stars;
        }

        public string Bar(double value, double max = 100, int width = 20)
        {
            int filled = (int)Math.Round(value / max * width);
            filled = Math.Clamp(filled, 0, width);
            return new string('█', filled) + new string('░', width - filled);
        }

        public IEnumerable<string> PlayByPlay =>
            BeatResults.SelectMany(b =>
                new[] { $"[{b.BeatLabel}]" }
                .Concat(b.Commentary)
                .Concat(new[] { $"  ▶ {b.StatsLine}", "" }));
    }

    /// <summary>
    /// The terms that make up <see cref="MatchEngineResult.FinalScore"/>. Weighted
    /// components first, then the nudges — they sum to the score before clamping.
    /// </summary>
    public class ScoreBreakdown
    {
        /// <summary>Saturated technical score × the match type's technical weight.</summary>
        public double Technical { get; init; }

        /// <summary>Saturated storytelling score × the match type's storytelling weight.</summary>
        public double Storytelling { get; init; }

        /// <summary>
        /// Normalised crowd reading × the crowd weight × <see cref="InvestmentFactor"/>.
        /// </summary>
        public double Crowd { get; init; }

        /// <summary>The crowd term before investment was applied. Crowd / this = the factor.</summary>
        public double CrowdBeforeInvestment { get; init; }

        /// <summary>How much of the crowd term investment kept — 1.0 is a typical room.</summary>
        public double InvestmentFactor { get; init; }

        public double FinishNudge    { get; init; }
        public double VarietyNudge   { get; init; }
        public double CoherenceNudge { get; init; }

        /// <summary>What investment was worth, in points of the final score.</summary>
        public double InvestmentPoints => Crowd - CrowdBeforeInvestment;

        /// <summary>Largest-first, for display. Nudges included, signed.</summary>
        public IEnumerable<(string Label, double Points)> Ordered =>
            new[]
            {
                ("Crowd",        Crowd),
                ("Storytelling", Storytelling),
                ("Technical",    Technical),
                ("Finish",       FinishNudge),
                ("Variety",      VarietyNudge),
                ("Match type",   CoherenceNudge)
            }
            .Where(x => Math.Abs(x.Item2) > 0.005)
            .OrderByDescending(x => Math.Abs(x.Item2));
    }

}