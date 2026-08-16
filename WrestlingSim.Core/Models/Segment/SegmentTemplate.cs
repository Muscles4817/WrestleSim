using WrestlingSim.Enums;

namespace WrestlingSim.Models.Segment
{
    /// <summary>
    /// A named, pre-built segment archetype — the segment-side equivalent of MatchStructure.
    /// Wraps one of the SegmentFactory builders with the metadata the booking UI needs:
    /// who it needs, what it costs, and what it stamps on the feud.
    /// </summary>
    public class SegmentTemplate
    {
        public required string Name        { get; init; }
        public required string Description { get; init; }
        public required string Category    { get; init; }
        public required SegmentType Type   { get; init; }
        public required SegmentLocation Location { get; init; }

        /// <summary>
        /// Prompt for each required participant, in the order the builder expects them.
        /// </summary>
        public required IReadOnlyList<string> ParticipantRoles { get; init; }

        /// <summary>
        /// Whether the builder takes an open-ended list beyond the required roles
        /// (a faction, a group of attackers).
        /// </summary>
        public bool AllowsExtraParticipants { get; init; }

        /// <summary>Label for the open-ended slots, e.g. "Attacker".</summary>
        public string ExtraParticipantRole { get; init; } = "Participant";

        /// <summary>Tags stamped on the feuds between participants when this is booked.</summary>
        public IReadOnlyList<FeudHistoryTag> HistoryTags { get; init; } = [];

        /// <summary>Whether the player should be prompted for promo dialogue.</summary>
        public bool UsesDialogue { get; init; }

        public string BookerTip { get; init; } = "";

        public IReadOnlyList<string> Tags { get; init; } = [];

        /// <summary>The underlying SegmentFactory call.</summary>
        public required Func<IReadOnlyList<Wrestler>, string, Segment> Build { get; init; }

        public int MinParticipants => ParticipantRoles.Count;

        /// <summary>
        /// Builds the segment and stamps this template's history tags onto it.
        /// </summary>
        public Segment Create(IReadOnlyList<Wrestler> participants, string dialogue = "")
        {
            if (participants.Count < MinParticipants)
                throw new ArgumentException(
                    $"{Name} needs at least {MinParticipants} participant(s), got {participants.Count}.");

            var segment = Build(participants, dialogue);
            segment.HistoryTags = HistoryTags.ToList();
            return segment;
        }

        public override string ToString() => Name;
    }
}
