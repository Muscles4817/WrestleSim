using WrestlingSim.Enums;

namespace WrestlingSim.Models.MatchPlan
{
    /// <summary>
    /// A named, reusable archetype for a single match beat.
    /// Templates live in BeatLibrary; players pick one and supply the
    /// context-dependent choices (control, optional overrides) to get a MatchBeat.
    /// </summary>
    public class BeatTemplate
    {
        public required string Name        { get; init; }
        public required string Description { get; init; }
        public required BeatType Type      { get; init; }
        public required string Category    { get; init; }

        public BeatIntensity DefaultIntensity { get; init; } = BeatIntensity.Medium;
        public BeatDuration  DefaultDuration  { get; init; } = BeatDuration.Medium;

        /// <summary>
        /// Short hint shown next to the template name in booking menus.
        /// E.g. "Works best after a Heat Segment" or "Requires feud: Building+".
        /// </summary>
        public string BookerTip { get; init; } = "";

        /// <summary>
        /// Tags for filtering (e.g. "Power", "Aerial", "Technical", "Feud").
        /// </summary>
        public IReadOnlyList<string> Tags { get; init; } = [];

        /// <summary>
        /// Minimum feud intensity required to book this beat.
        /// Matches the gating enforced by MatchPlan.Validate().
        /// </summary>
        public FeudIntensity RequiredFeudIntensity { get; init; } = FeudIntensity.None;

        /// <summary>
        /// When set, the engine uses this style's skill stat instead of the control
        /// wrestler's natural style, making template choice mechanically distinct.
        /// E.g. Technical Dissection sets Technical; Power Beatdown sets Powerhouse.
        /// </summary>
        public WrestlingStyle? StyleHint { get; init; }

        // ── Factory ──────────────────────────────────────────────────────────

        /// <summary>
        /// Instantiates a MatchBeat from this template.
        /// Control is the only required player choice; intensity and duration
        /// can be overridden from the template defaults.
        /// </summary>
        public MatchBeat ToMatchBeat(
            BeatControl      control,
            BeatIntensity?   intensity        = null,
            BeatDuration?    duration         = null,
            FeudalResonance? feudalResonance  = null) => new MatchBeat
        {
            Type            = Type,
            Control         = control,
            Intensity       = intensity       ?? DefaultIntensity,
            Duration        = duration        ?? DefaultDuration,
            FeudalResonance = feudalResonance,
            StyleHint       = StyleHint
        };

        public override string ToString() => Name;
    }
}
