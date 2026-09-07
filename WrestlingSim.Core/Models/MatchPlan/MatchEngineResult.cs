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

        /// <summary>
        /// Moments where somebody put a grudge ahead of winning — a cover broken out of
        /// spite, a winnable position abandoned to chase a rival, two rivals wiping each
        /// other out.
        ///
        /// Recorded so the *consequence* can outlive the match. Doc 18 §2.5: the reason a
        /// booker runs two rivals into a multi-man match is that it advances their story
        /// without spending the singles match, and "you cost me the title" is one of
        /// wrestling's most reliable escalators. That only works if being cost the match
        /// actually feeds the feud, which needs the match to say who did what to whom.
        /// </summary>
        public List<GrudgeMoment> GrudgeMoments { get; init; } = new();

        /// <summary>
        /// Who went out, in the order they went out, last one first — so reading it top to
        /// bottom is the story working backwards from the win.
        ///
        /// Empty for a match with no eliminations in it. The order rather than the set,
        /// because doc 18 §2.5 puts the drama of an elimination match in the order, and a
        /// result that only said who won would be reporting the least interesting fact
        /// about it.
        /// </summary>
        public List<EliminatedSide> Eliminations { get; init; } = new();

        /// <summary>
        /// How well the falls were spaced, 0–1. 1.0 is evenly spread or better, 0.0 is falls
        /// with nothing between them. See <see cref="Engine.MatchEngine.EliminationPacing"/>.
        /// Always 1.0 when nobody was eliminated.
        /// </summary>
        public double EliminationPacing { get; init; } = 1.0;

        /// <summary>
        /// Who was still standing when it ended, on the winning side.
        ///
        /// The Survivor Series payoff, and the reason that match has a name of its own: the
        /// story is not that a team won, it is *who was left*. A sole survivor is a made
        /// wrestler; four survivors is a squash of the other team. Empty outside an
        /// elimination match of teams.
        /// </summary>
        public List<Wrestler> Survivors { get; init; } = new();

        /// <summary>
        /// In a handicap match, how much of it the outnumbered side spent fighting rather
        /// than being beaten up, 0–1. Zero in every other match.
        ///
        /// The measure the format is actually graded on, because doc 18 §2.5 says the
        /// statement is about the lone wrestler's toughness rather than the outcome — so a
        /// valiant loss reads well here and a squash reads badly, which is the right way
        /// round and not something the winner can tell you.
        /// </summary>
        public double Defiance { get; init; }

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
        /// Reported rather than kept private for two reasons. The player one, which is an
        /// intention and not yet a description: a rating with no breakdown is a verdict, not
        /// feedback — a booker who cannot see that a match lost four points on variety cannot
        /// learn to book a better one. **Nothing in the UI reads this yet**, so the sentence
        /// was written in the present tense about something that has not been built; review
        /// caught it. The engineering reason is live today: it makes the composite
        /// *testable*. Every claim about what a term does to a
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

        /// <summary>What the spacing of the eliminations was worth. Zero in every other match.</summary>
        public double PacingNudge    { get; init; }

        /// <summary>What the lone wrestler's resistance was worth. Zero outside a handicap match.</summary>
        public double DefianceNudge  { get; init; }

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
                ("Match type",   CoherenceNudge),
                ("Fall spacing", PacingNudge),
                ("Defiance",     DefianceNudge)
            }
            .Where(x => Math.Abs(x.Item2) > 0.005)
            .OrderByDescending(x => Math.Abs(x.Item2));
    }

    /// <summary>One wrestler's exit from an elimination match, and who did it.</summary>
    public class EliminatedSide
    {
        /// <summary>The one who went out.</summary>
        public required Wrestler Wrestler { get; init; }

        /// <summary>Who scored the fall.</summary>
        public required Wrestler By { get; init; }

        /// <summary>Which elimination this was, 1-based. The order is the story.</summary>
        public int Order { get; init; }

        /// <summary>
        /// How many wrestlers were still in the match after this one went out.
        ///
        /// People, not sides. Sides was right for a triple threat, where a side is one
        /// wrestler — and useless the moment teams arrived: in a Survivor Series both sides
        /// are in until the last fall, so the eliminations panel counted "2 left" six times
        /// running and told the reader nothing.
        /// </summary>
        public int Remaining { get; init; }
    }


}
