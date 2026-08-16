using WrestlingSim.Enums;

namespace WrestlingSim.Models.MatchPlan
{
    public class BeatResult
    {
        public BeatType BeatType { get; set; }
        public BeatControl Control { get; set; }

        public List<string> Commentary { get; set; } = new();

        // Raw deltas applied this beat
        public double CrowdEnergyDelta { get; set; }
        public double MomentumDelta { get; set; }
        public double TechnicalContribution { get; set; }
        public double StorytellingContribution { get; set; }

        // State snapshots after this beat resolves
        public double CrowdEnergyAfter { get; set; }
        public double MomentumAfter { get; set; }
        public double TechnicalScoreAfter { get; set; }
        public double StorytellingScoreAfter { get; set; }

        public bool FeudalResonanceActivated { get; set; }

        // ── Display ──────────────────────────────────────────────────────────

        public string BeatLabel => BeatType switch
        {
            BeatType.HotOpening          => "HOT OPENING",
            BeatType.SlowOpening         => "SLOW OPENING",
            BeatType.StandardOpening     => "OPENING",
            BeatType.HeatSegment         => "HEAT SEGMENT",
            BeatType.Comeback            => "COMEBACK",
            BeatType.RestHold            => "REST HOLD",
            BeatType.HighSpot            => "HIGH SPOT",
            BeatType.CrowdBrawl          => "CROWD BRAWL",
            BeatType.PsychologicalWarfare => "PSYCHOLOGICAL WARFARE",
            BeatType.RevengeSpot         => "REVENGE SPOT",
            BeatType.FeudalEscalation    => "FEUDAL ESCALATION",
            BeatType.ThirdPartyPullIn    => "THIRD PARTY PULL-IN",
            BeatType.AlliesRejected      => "GOES IT ALONE",
            BeatType.NearFall            => "NEAR FALL",
            BeatType.FinishClean         => "FINISH — CLEAN",
            BeatType.FinishRollup        => "FINISH — ROLL-UP",
            BeatType.FinishSubmission    => "FINISH — SUBMISSION",
            BeatType.FinishDQ            => "FINISH — DISQUALIFICATION",
            BeatType.FinishCountout      => "FINISH — COUNT-OUT",
            BeatType.FinishInterference  => "FINISH — INTERFERENCE",
            BeatType.FinishSuperFinisher => "FINISH — SUPER FINISHER",
            _                            => BeatType.ToString().ToUpper()
        };

        public string StatsLine
        {
            get
            {
                double before = CrowdEnergyAfter - CrowdEnergyDelta;
                string feudTag = FeudalResonanceActivated ? "  ★ Feud Resonance" : "";
                return $"Crowd: {before:F0}→{CrowdEnergyAfter:F0}  |  " +
                       $"Momentum: {MomentumAfter:+0.0;-0.0;0.0}  |  " +
                       $"+Tech: {TechnicalContribution:F1}  |  " +
                       $"+Story: {StorytellingContribution:F1}" +
                       feudTag;
            }
        }
    }
}
