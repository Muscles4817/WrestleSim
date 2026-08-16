using WrestlingSim.Enums;

namespace WrestlingSim.Models.MatchPlan
{
    public class MatchBeat
    {
        public BeatType Type { get; set; }

        // Which wrestlers are active in this beat.
        // Defaults to both match participants; override to include run-ins, managers, etc.
        public List<Wrestler> Participants { get; set; } = new();

        // Who has the advantage / is driving the action in this beat.
        public BeatControl Control { get; set; }

        public BeatIntensity Intensity { get; set; } = BeatIntensity.Medium;
        public BeatDuration Duration { get; set; } = BeatDuration.Medium;

        // Optional: narrative/feud context that amplifies this beat when satisfied.
        public FeudalResonance? FeudalResonance { get; set; }

        /// <summary>
        /// When set, overrides the control wrestler's natural style for skill lookups
        /// in this beat. Set by BeatTemplate so template choice is mechanically meaningful
        /// (e.g. Technical Dissection uses Technical skill regardless of wrestler style).
        /// </summary>
        public WrestlingStyle? StyleHint { get; set; }

        // ── Derived helpers ──────────────────────────────────────────────────

        public double IntensityModifier => Intensity switch
        {
            BeatIntensity.Low     => 0.6,
            BeatIntensity.Medium  => 1.0,
            BeatIntensity.High    => 1.3,
            BeatIntensity.Extreme => 1.6,
            _                     => 1.0
        };

        public double DurationModifier => Duration switch
        {
            BeatDuration.Brief    => 0.5,
            BeatDuration.Short    => 0.8,
            BeatDuration.Medium   => 1.0,
            BeatDuration.Long     => 1.3,
            BeatDuration.Extended => 1.6,
            _                     => 1.0
        };

        /// <summary>
        /// Wall-clock cost of this beat, spent against the show's runtime budget.
        /// Mirrors the ranges documented on BeatDuration.
        /// </summary>
        public int DurationMinutes => Duration switch
        {
            BeatDuration.Brief    => 1,
            BeatDuration.Short    => 2,
            BeatDuration.Medium   => 4,
            BeatDuration.Long     => 7,
            BeatDuration.Extended => 10,
            _                     => 4
        };

        public bool IsFinish => Type is
            BeatType.FinishClean or BeatType.FinishRollup or BeatType.FinishSubmission or
            BeatType.FinishDQ or BeatType.FinishCountout or BeatType.FinishInterference or
            BeatType.FinishSuperFinisher;

        public bool IsOpening => Type is
            BeatType.HotOpening or BeatType.SlowOpening or BeatType.StandardOpening;

        /// <summary>
        /// Independent copy. Structures in MatchStructureLibrary are static singletons,
        /// so a plan built from a preset must clone its beats or editing the plan
        /// writes straight through into the library.
        /// </summary>
        public MatchBeat Clone() => new MatchBeat
        {
            Type            = Type,
            Participants    = new List<Wrestler>(Participants),
            Control         = Control,
            Intensity       = Intensity,
            Duration        = Duration,
            FeudalResonance = FeudalResonance,
            StyleHint       = StyleHint
        };
    }
}
