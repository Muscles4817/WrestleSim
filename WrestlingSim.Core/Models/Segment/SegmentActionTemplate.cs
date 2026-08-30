using WrestlingSim.Enums;

namespace WrestlingSim.Models.Segment
{
    /// <summary>
    /// A named, reusable archetype for a single segment action — the segment-side
    /// equivalent of BeatTemplate. Templates live in SegmentActionLibrary; the player
    /// picks one and supplies the performer, target and dialogue.
    /// </summary>
    public class SegmentActionTemplate
    {
        public required string Name        { get; init; }
        public required string Description { get; init; }
        public required string Category    { get; init; }
        public required SegmentActionType ActionType { get; init; }

        /// <summary>Crowd reaction before charisma, location and delivery.</summary>
        public double BaseImpact { get; init; } = 2.0;

        /// <summary>Feud heat this action deposits.</summary>
        public double Heat { get; init; }

        /// <summary>Multiplier on the performer's natural overness gain from this action.</summary>
        public double OvernessScale { get; init; } = 1.0;

        /// <summary>History tag stamped on the feud when this action is booked.</summary>
        public FeudHistoryTag? HistoryTag { get; init; }

        public string BookerTip { get; init; } = "";

        public IReadOnlyList<string> Tags { get; init; } = [];

        public bool RequiresTarget => ActionType is
            SegmentActionType.Attack or SegmentActionType.Betrayal;

        // ── Factory ──────────────────────────────────────────────────────────

        public SegmentAction ToAction(Wrestler performer, Wrestler? target = null, string dialogue = "") =>
            new SegmentAction
            {
                ActionType     = ActionType,
                Performer      = performer,
                Target         = target,
                Dialogue       = dialogue,
                BaseImpact     = BaseImpact,
                HeatImpact     = Heat,
                OvernessImpact = OvernessFor(performer),
                Label          = Name
            };

        private double OvernessFor(Wrestler performer)
        {
            double impact = 0.5;

            if (ActionType is SegmentActionType.Talk or SegmentActionType.Interrupt)
                impact += performer.Charisma * 0.4;

            if (ActionType is SegmentActionType.Attack or SegmentActionType.Betrayal)
                impact += 1.0 + (performer.Physical?.Strength ?? 0) / 100.0;

            return Math.Clamp(impact * OvernessScale, 0.5, 3.0);
        }

        public override string ToString() => Name;
    }
}
