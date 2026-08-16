using WrestlingSim.Enums;

namespace WrestlingSim.Models.MatchPlan
{
    /// <summary>
    /// Optional narrative context attached to a beat.
    /// When RequiredHistoryTags are satisfied by the active feud, the beat
    /// receives amplified crowd and storytelling scores.
    /// </summary>
    public class FeudalResonance
    {
        public FeudalResonanceType ResonanceType { get; set; }

        // All of these must appear in Feud.History for full amplification.
        // Empty list = any feud is sufficient.
        public List<FeudHistoryTag> RequiredHistoryTags { get; set; } = new();

        public bool IsSatisfiedBy(Feud? feud)
        {
            if (feud == null || feud.Intensity == FeudIntensity.None)
                return false;

            return !RequiredHistoryTags.Any()
                || RequiredHistoryTags.All(feud.HasTag);
        }
    }
}
