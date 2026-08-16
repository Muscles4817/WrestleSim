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

        // Final rating
        public double FinalScore  { get; init; }  // 0–100
        public double StarRating  { get; init; }  // 0–5

        // ── Display helpers ──────────────────────────────────────────────────

        public string StarDisplay
        {
            get
            {
                // Full stars
                int full = (int)StarRating;
                double rem = StarRating - full;

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
                if (stars.Replace("¼","").Replace("½","").Replace("¾","").Length > 5)
                    stars = "★★★★★";

                return $"{stars}  ({StarRating:F2} / 5.00)";
            }
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
