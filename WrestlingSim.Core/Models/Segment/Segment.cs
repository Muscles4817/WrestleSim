using WrestlingSim.Enums;

namespace WrestlingSim.Models.Segment
{
    public class Segment : ICardItem
    {
        public string Name { get; set; }
        public SegmentType Type { get; set; } // Promo, Brawl, etc.
        public SegmentLocation Location { get; set; }
        public List<SegmentAction> Actions { get; set; } = new();
        public List<Wrestler> Participants { get; set; } = new();
        /// <summary>
        /// How much of it is written (docs/gimmick-plan.md Part 3). Neither form is safer or
        /// better by rule; what each gets depends on the writers and the people talking.
        /// </summary>
        public PromoForm Form { get; set; } = PromoForm.Scripted;

        /// <summary>The old two-way switch, kept for the console and old saves: scripted, or handed the microphone.</summary>
        public bool IsScripted
        {
            get => Form == PromoForm.Scripted;
            set => Form = value ? PromoForm.Scripted : PromoForm.Free;
        }

        /// <summary>What it physically asks of them. Null reads it from the segment's type and place.</summary>
        public PromoStaging? Staging { get; set; }

        /// <summary>What it is meant to make the crowd feel. Null lets each speaker play their own natural note.</summary>
        public PromoRegister? Register { get; set; }

        /// <summary>Somebody this segment turns (docs/gimmick-plan.md Part 5), or null.</summary>
        public AlignmentMove? Turn { get; set; }

        /// <summary>Somebody this segment teases a turn for, or null.</summary>
        public AlignmentMove? Tease { get; set; }
        public double AudienceImpact { get; set; } // Set by SegmentSimulator
        public double HeatImpact { get; set; } // Feud heat generated

        /// <summary>
        /// History tags this segment stamps onto the feuds between its participants.
        /// Set from the template it was booked from; a Betrayal segment stamps Betrayal.
        /// </summary>
        public List<FeudHistoryTag> HistoryTags { get; set; } = new();

        public Segment(string name, SegmentType type, SegmentLocation location, bool isScripted)
        {
            Name = name;
            Type = type;
            Location = location;
            IsScripted = isScripted;
        }

        // ── ICardItem ────────────────────────────────────────────────────────

        public CardItemKind Kind => CardItemKind.Segment;

        /// <summary>
        /// Somebody to be in it and something for them to do. A segment with a cast and no
        /// actions is a person standing in the ring in silence.
        /// </summary>
        public bool IsComplete => Participants.Count > 0 && Actions.Count > 0;

        /// <summary>
        /// A base runtime for the format plus a minute per beat of action.
        /// A one-line promo is quick; a contract signing that ends in a brawl is not.
        /// </summary>
        public int DurationMinutes
        {
            get
            {
                int baseMinutes = Type switch
                {
                    SegmentType.Promo           => 3,
                    SegmentType.Confrontation   => 4,
                    SegmentType.ContractSigning => 5,
                    SegmentType.Celebration     => 4,
                    SegmentType.Brawl           => 2,
                    SegmentType.SurpriseReturn  => 2,
                    _                           => 3
                };
                return Math.Clamp(baseMinutes + Actions.Count, 2, 15);
            }
        }

        IReadOnlyList<Wrestler> ICardItem.Wrestlers => Participants;

        // ── Building ─────────────────────────────────────────────────────────

        public void AddParticipant(Wrestler wrestler)
        {
            if (!Participants.Contains(wrestler))
                Participants.Add(wrestler);
        }

        public void AddAction(SegmentAction action)
        {
            Actions.Add(action);
        }

        /// <summary>
        /// Returns validation errors. Empty list = the segment is bookable.
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (Participants.Count == 0)
                errors.Add("Segment has no participants.");

            if (Actions.Count == 0)
                errors.Add("Segment has no actions — nothing happens.");

            foreach (var action in Actions)
            {
                if (action.Performer == null)
                {
                    errors.Add($"An action ({action.ActionType}) has no performer.");
                    continue;
                }

                if (!Participants.Contains(action.Performer))
                    errors.Add($"{action.Performer.RingName} performs an action but is not a participant.");

                if (action.RequiresTarget && action.Target == null)
                    errors.Add($"{action.ActionType} by {action.Performer.RingName} needs a target.");

                if (action.Target != null && action.Target == action.Performer)
                    errors.Add($"{action.Performer.RingName} cannot target themselves.");
            }

            return errors;
        }
    }
}
