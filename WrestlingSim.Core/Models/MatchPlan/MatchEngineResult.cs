namespace WrestlingSim.Models.MatchPlan
{
    public class MatchEngineResult
    {
        public required Wrestler Winner { get; init; }
        public required Wrestler Loser  { get; init; }

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

        // Final rating
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
}
