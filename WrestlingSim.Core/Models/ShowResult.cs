using WrestlingSim.Engine;
using WrestlingSim.Models.MatchPlan;
using SegResult = WrestlingSim.Models.Segment.SegmentResult;

namespace WrestlingSim.Models
{
    public class ShowResult
    {
        /// <summary>Weighted overall show score, 0–100.</summary>
        public double OverallRating { get; set; }

        public List<CardItemResult> Items { get; set; } = new();

        /// <summary>Every feud that moved as a result of this show.</summary>
        public List<FeudUpdate> FeudUpdates { get; set; } = new();

        /// <summary>
        /// Who gained and who lost standing tonight. This is where a result stops being a
        /// star rating and starts being a consequence.
        /// </summary>
        public List<StatusChange> StatusChanges { get; set; } = new();

        /// <summary>
        /// Every championship that moved tonight — changes, defences, and the quiet
        /// erosion of a champion losing somewhere the belt was not on the line.
        /// </summary>
        public List<TitleUpdate> TitleUpdates { get; set; } = new();

        public int BookedMinutes { get; set; }
        public int BudgetMinutes { get; set; }

        /// <summary>Fraction shaved off the overall score for running long, 0–0.35.</summary>
        public double OverrunPenalty { get; set; }

        /// <summary>Crowd mood at the final bell, 0–10.</summary>
        public double FinalCrowdMood { get; set; }

        /// <summary>Kept for compatibility with the old label-to-score view.</summary>
        public Dictionary<string, double> Breakdown =>
            Items.ToDictionary(i => i.Label, i => i.Score);
    }

    public class CardItemResult
    {
        public required string Label { get; init; }
        public required CardItemKind Kind { get; init; }

        /// <summary>Score after every modifier, 0–100.</summary>
        public double Score { get; set; }

        /// <summary>Score before fatigue, crowd mood and position weighting.</summary>
        public double RawScore { get; set; }

        public double PositionWeight { get; set; } = 1.0;
        public bool FatiguePenaltyApplied { get; set; }
        public int DurationMinutes { get; set; }

        /// <summary>Crowd mood going into this item, 0–10.</summary>
        public double CrowdMoodBefore { get; set; }

        /// <summary>Full engine output when this item was a match.</summary>
        public MatchEngineResult? MatchResult { get; set; }

        /// <summary>Full simulator output when this item was a segment.</summary>
        public SegResult? SegmentResult { get; set; }

        /// <summary>
        /// The match's star rating. Normally derived from <see cref="MatchResult"/>, but
        /// settable so a loaded save can restore it: the full engine result is deliberately
        /// not persisted, so without this a reloaded show's card rendered with no stars.
        /// </summary>
        public double? StarRating
        {
            get => _starRating ?? MatchResult?.StarRating;
            set => _starRating = value;
        }
        private double? _starRating;

        public List<string> Notes { get; set; } = new();
    }
}
