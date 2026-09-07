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

        /// <summary>
        /// What kind of noise this beat drew, when the handler knows better than the
        /// engine's default reading of the delta's sign.
        ///
        /// Most beats leave this null and are classified from context — a positive delta
        /// from somebody the crowd likes is a pop, one from somebody they want beaten is
        /// heat. The beats that set it explicitly are the ones where the sign lies: a
        /// denied tag takes energy *out* of the room and that is the audience holding its
        /// breath, not leaving.
        /// </summary>
        public ReactionKind? Reaction { get; set; }

        /// <summary>The reaction actually recorded, once the engine has classified it.</summary>
        public ReactionKind ResolvedReaction { get; set; }
        public double AdvantageDelta { get; set; }
        public double TechnicalContribution { get; set; }
        public double StorytellingContribution { get; set; }

        // State snapshots after this beat resolves
        public double CrowdEnergyAfter { get; set; }

        /// <summary>
        /// Crowd energy as the beat began. Recorded rather than derived: ApplyEnergy
        /// compresses gains near the ceiling and clamps, and decay runs between beats, so
        /// "after minus delta" reported a starting figure the crowd was never at.
        /// </summary>
        public double CrowdEnergyBefore { get; set; }
        public double AdvantageAfter { get; set; }
        public double TechnicalScoreAfter { get; set; }
        public double StorytellingScoreAfter { get; set; }

        public bool FeudalResonanceActivated { get; set; }

        /// <summary>
        /// 1.0 the first time a beat type is used, falling with each repetition in the
        /// same match. Exposed so the booking UI can show why a repeated beat landed flat.
        /// </summary>
        public double RepetitionFactor { get; set; } = 1.0;

        /// <summary>
        /// What a near fall kept because a third party was, or was not, free to break it.
        /// 1.0 when the count carried real jeopardy; <see cref="Engine.MatchEngine
        /// .CrowdedOutNearFall"/> when somebody upright could have broken it. Left at 1.0 on
        /// every other beat type.
        ///
        /// Recorded rather than folded silently into the energy, because it could not be
        /// tested otherwise: a test comparing two matches measures the crowd level the
        /// eliminations built as much as the rule, which is how the first attempt at this
        /// passed against a mutation that reverted the mechanism entirely.
        /// </summary>
        public double NearFallJeopardy { get; set; } = 1.0;

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
                double before = CrowdEnergyBefore;
                string feudTag = FeudalResonanceActivated ? "  ★ Feud Resonance" : "";
                return $"Crowd: {before:F0}→{CrowdEnergyAfter:F0}  |  " +
                       $"Advantage: {AdvantageAfter:+0.0;-0.0;0.0}  |  " +
                       $"+Tech: {TechnicalContribution:F1}  |  " +
                       $"+Story: {StorytellingContribution:F1}" +
                       feudTag;
            }
        }
    }
}
